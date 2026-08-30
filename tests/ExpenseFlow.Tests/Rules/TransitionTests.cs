using System;
using System.Linq;
using ExpenseFlow.Domain.Enums;
using ExpenseFlow.Domain.Workflow;
using ExpenseFlow.Tests.Support;
using Xunit;

namespace ExpenseFlow.Tests.Rules
{
    /// <summary>
    /// What actually changes on the claim when a transition is applied,
    /// including the audit trail. The history entries matter: they are the
    /// only record of who did what, and a migration that quietly stops
    /// writing them would otherwise go unnoticed.
    /// </summary>
    public class TransitionTests
    {
        [Fact]
        public void Submitting_sets_the_status_total_and_timestamp()
        {
            var alice = TestData.Employee();
            var claim = TestData.SubmittableClaim(alice);   // 18.40 + 12.75

            ClaimWorkflow.Submit(claim, alice);

            Assert.Equal(ClaimStatus.Submitted, claim.StatusValue);
            Assert.Equal(31.15m, claim.TotalAmount);
            Assert.True(claim.SubmittedUtc.HasValue);
        }

        [Fact]
        public void Submitting_recalculates_the_total_rather_than_trusting_the_stored_value()
        {
            var alice = TestData.Employee();
            var claim = TestData.SubmittableClaim(alice);
            claim.TotalAmount = 9999m;   // stale

            ClaimWorkflow.Submit(claim, alice);

            Assert.Equal(31.15m, claim.TotalAmount);
        }

        [Fact]
        public void Submitting_writes_a_Submitted_history_entry()
        {
            var alice = TestData.Employee();
            var claim = TestData.SubmittableClaim(alice);

            ClaimWorkflow.Submit(claim, alice);

            var entry = Assert.Single(claim.History);
            Assert.Equal("Submitted", entry.Action);
            Assert.Equal(alice.Id, entry.ActorEmployeeId);
        }

        [Fact]
        public void Resubmitting_a_rejected_claim_is_recorded_as_Resubmitted()
        {
            var alice = TestData.Employee();
            var claim = TestData.Claim(alice, ClaimStatus.Rejected);
            claim.Lines.Add(TestData.Line(TestData.Mileage(), 10m));

            ClaimWorkflow.Submit(claim, alice);

            Assert.Contains(claim.History, h => h.Action == "Resubmitted");
            Assert.DoesNotContain(claim.History, h => h.Action == "Submitted");
        }

        [Fact]
        public void Resubmitting_clears_the_previous_decision()
        {
            var alice = TestData.Employee();
            var claim = TestData.Claim(alice, ClaimStatus.Rejected);
            claim.Lines.Add(TestData.Line(TestData.Mileage(), 10m));
            claim.RejectionReason = "no receipt attached";
            claim.DecidedUtc = DateTime.UtcNow.AddDays(-1);
            claim.DecidedByEmployeeId = 2;

            ClaimWorkflow.Submit(claim, alice);

            Assert.Null(claim.RejectionReason);
            Assert.Null(claim.DecidedUtc);
            Assert.Null(claim.DecidedByEmployeeId);
        }

        [Fact]
        public void Approving_records_the_decision_and_who_made_it()
        {
            var approver = TestData.Approver();
            var claim = TestData.Claim(TestData.Employee(), ClaimStatus.Submitted);

            ClaimWorkflow.Approve(claim, approver, "fine by me");

            Assert.Equal(ClaimStatus.Approved, claim.StatusValue);
            Assert.Equal(approver.Id, claim.DecidedByEmployeeId);
            Assert.True(claim.DecidedUtc.HasValue);
            Assert.Null(claim.RejectionReason);
            Assert.Contains(claim.History, h => h.Action == "Approved" && h.Comment == "fine by me");
        }

        [Fact]
        public void Rejecting_stores_the_reason_on_the_claim_and_in_history()
        {
            var approver = TestData.Approver();
            var claim = TestData.Claim(TestData.Employee(), ClaimStatus.Submitted);

            ClaimWorkflow.Reject(claim, approver, "receipt is unreadable");

            Assert.Equal(ClaimStatus.Rejected, claim.StatusValue);
            Assert.Equal("receipt is unreadable", claim.RejectionReason);
            Assert.Contains(claim.History, h => h.Action == "Rejected" && h.Comment == "receipt is unreadable");
        }

        [Fact]
        public void A_rejected_claim_becomes_editable_again()
        {
            var claim = TestData.Claim(TestData.Employee(), ClaimStatus.Submitted);

            ClaimWorkflow.Reject(claim, TestData.Approver(), "nope");

            Assert.True(claim.IsEditable);
        }

        [Fact]
        public void The_full_lifecycle_accumulates_an_audit_trail()
        {
            var alice = TestData.Employee();
            var bob = TestData.Approver();
            var dana = TestData.Admin();

            var claim = TestData.SubmittableClaim(alice);

            ClaimWorkflow.Submit(claim, alice);
            ClaimWorkflow.Reject(claim, bob, "needs a receipt");
            ClaimWorkflow.Submit(claim, alice);
            ClaimWorkflow.Approve(claim, bob, null);
            ClaimWorkflow.Reimburse(claim, dana);

            Assert.Equal(ClaimStatus.Reimbursed, claim.StatusValue);
            Assert.Equal(
                new[] { "Submitted", "Rejected", "Resubmitted", "Approved", "Reimbursed" },
                claim.History.Select(h => h.Action).ToArray());
        }

        // ---------- reimbursement ----------

        [Fact]
        public void Only_an_admin_can_reimburse()
        {
            var claim = TestData.Claim(TestData.Employee(), ClaimStatus.Approved);

            Assert.True(ClaimWorkflow.CanReimburse(claim, TestData.Admin()).IsAllowed);
            Assert.False(ClaimWorkflow.CanReimburse(claim, TestData.Approver()).IsAllowed);
            Assert.False(ClaimWorkflow.CanReimburse(claim, TestData.Employee()).IsAllowed);
        }

        [Theory]
        [InlineData(ClaimStatus.Draft)]
        [InlineData(ClaimStatus.Submitted)]
        [InlineData(ClaimStatus.Rejected)]
        [InlineData(ClaimStatus.Reimbursed)]
        public void Only_an_approved_claim_can_be_reimbursed(ClaimStatus status)
        {
            var claim = TestData.Claim(TestData.Employee(), status);

            Assert.False(ClaimWorkflow.CanReimburse(claim, TestData.Admin()).IsAllowed);
        }
    }
}
