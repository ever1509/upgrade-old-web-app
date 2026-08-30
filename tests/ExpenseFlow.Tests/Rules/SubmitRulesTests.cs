using System.Linq;
using ExpenseFlow.Domain.Enums;
using ExpenseFlow.Domain.Workflow;
using ExpenseFlow.Tests.Support;
using Xunit;

namespace ExpenseFlow.Tests.Rules
{
    /// <summary>
    /// Characterization tests for ClaimWorkflow.CanSubmit.
    ///
    /// These describe the behaviour of the LEGACY app as it exists today.
    /// They are not aspirational - if one of them fails after a migration
    /// step, the migration changed behaviour and that is a bug, even if the
    /// new behaviour looks more sensible.
    /// </summary>
    public class SubmitRulesTests
    {
        private readonly ApprovalPolicy _policy = new ApprovalPolicy();

        [Fact]
        public void A_claim_with_no_lines_cannot_be_submitted()
        {
            var claim = TestData.Claim(TestData.Employee());

            var result = ClaimWorkflow.CanSubmit(claim, _policy);

            Assert.False(result.IsAllowed);
            Assert.Contains(result.Errors, e => e.Contains("at least one expense line"));
        }

        [Fact]
        public void A_claim_with_no_title_cannot_be_submitted()
        {
            var claim = TestData.Claim(TestData.Employee(), title: "   ");
            claim.Lines.Add(TestData.Line(TestData.Meals(), 10m));

            Assert.False(ClaimWorkflow.CanSubmit(claim, _policy).IsAllowed);
        }

        [Fact]
        public void A_complete_draft_can_be_submitted()
        {
            var claim = TestData.SubmittableClaim(TestData.Employee());

            Assert.True(ClaimWorkflow.CanSubmit(claim, _policy).IsAllowed);
        }

        // ---------- receipt thresholds ----------

        [Theory]
        [InlineData(10.00, true)]   // well under the 25.00 limit
        [InlineData(24.99, true)]
        [InlineData(25.00, true)]   // exactly at the limit: still allowed
        [InlineData(25.01, false)]  // one cent over: receipt required
        [InlineData(60.00, false)]
        public void Meals_requires_a_receipt_only_above_its_limit(decimal amount, bool allowed)
        {
            var claim = TestData.Claim(TestData.Employee());
            claim.Lines.Add(TestData.Line(TestData.Meals(), amount));

            Assert.Equal(allowed, ClaimWorkflow.CanSubmit(claim, _policy).IsAllowed);
        }

        [Fact]
        public void An_over_limit_line_is_allowed_once_a_receipt_is_attached()
        {
            var claim = TestData.Claim(TestData.Employee());
            claim.Lines.Add(TestData.Line(TestData.Meals(), 60m, withReceipt: true));

            Assert.True(ClaimWorkflow.CanSubmit(claim, _policy).IsAllowed);
        }

        [Fact]
        public void The_receipt_error_names_the_category_and_its_limit()
        {
            var claim = TestData.Claim(TestData.Employee());
            claim.Lines.Add(TestData.Line(TestData.Meals(), 60m));

            var error = ClaimWorkflow.CanSubmit(claim, _policy).FirstError;

            Assert.Contains("needs a receipt", error);
            Assert.Contains("Meals", error);
            Assert.Contains("25.00", error);
        }

        [Fact]
        public void Travel_always_requires_a_receipt_however_small()
        {
            var claim = TestData.Claim(TestData.Employee());
            claim.Lines.Add(TestData.Line(TestData.Travel(), 0.50m));

            Assert.False(ClaimWorkflow.CanSubmit(claim, _policy).IsAllowed);
        }

        [Fact]
        public void Mileage_never_requires_a_receipt()
        {
            var claim = TestData.Claim(TestData.Employee());
            claim.Lines.Add(TestData.Line(TestData.Mileage(), 900m));

            Assert.True(ClaimWorkflow.CanSubmit(claim, _policy).IsAllowed);
        }

        // ---------- amounts and dates ----------

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void Every_line_must_have_a_positive_amount(decimal amount)
        {
            var claim = TestData.Claim(TestData.Employee());
            claim.Lines.Add(TestData.Line(TestData.Mileage(), amount));

            Assert.False(ClaimWorkflow.CanSubmit(claim, _policy).IsAllowed);
        }

        [Fact]
        public void A_line_dated_today_is_accepted()
        {
            var claim = TestData.Claim(TestData.Employee());
            claim.Lines.Add(TestData.Line(TestData.Mileage(), 10m, daysAgo: 0));

            Assert.True(ClaimWorkflow.CanSubmit(claim, _policy).IsAllowed);
        }

        [Fact]
        public void A_line_dated_in_the_future_is_rejected()
        {
            var claim = TestData.Claim(TestData.Employee());
            claim.Lines.Add(TestData.Line(TestData.Mileage(), 10m, daysAgo: -5));

            var result = ClaimWorkflow.CanSubmit(claim, _policy);

            Assert.False(result.IsAllowed);
            Assert.Contains(result.Errors, e => e.Contains("future"));
        }

        [Fact]
        public void A_claim_over_the_maximum_amount_is_rejected()
        {
            var claim = TestData.Claim(TestData.Employee());
            claim.Lines.Add(TestData.Line(TestData.Mileage(), _policy.MaxClaimAmount + 0.01m));

            Assert.False(ClaimWorkflow.CanSubmit(claim, _policy).IsAllowed);
        }

        [Fact]
        public void A_claim_with_too_many_lines_is_rejected()
        {
            var claim = TestData.Claim(TestData.Employee());
            for (var i = 0; i < _policy.MaxLinesPerClaim + 1; i++)
                claim.Lines.Add(TestData.Line(TestData.Mileage(), 1m));

            Assert.False(ClaimWorkflow.CanSubmit(claim, _policy).IsAllowed);
        }

        // ---------- status gating ----------

        [Theory]
        [InlineData(ClaimStatus.Draft, true)]
        [InlineData(ClaimStatus.Rejected, true)]     // resubmission is allowed
        [InlineData(ClaimStatus.Submitted, false)]
        [InlineData(ClaimStatus.Approved, false)]
        [InlineData(ClaimStatus.Reimbursed, false)]
        public void Only_draft_and_rejected_claims_can_be_submitted(ClaimStatus status, bool allowed)
        {
            var claim = TestData.Claim(TestData.Employee(), status);
            claim.Lines.Add(TestData.Line(TestData.Mileage(), 10m));

            Assert.Equal(allowed, ClaimWorkflow.CanSubmit(claim, _policy).IsAllowed);
        }

        [Fact]
        public void A_null_claim_is_denied_rather_than_throwing()
        {
            var result = ClaimWorkflow.CanSubmit(null, _policy);

            Assert.False(result.IsAllowed);
            Assert.Equal("Claim not found.", result.FirstError);
        }

        [Fact]
        public void A_null_policy_falls_back_to_the_defaults()
        {
            var claim = TestData.SubmittableClaim(TestData.Employee());

            Assert.True(ClaimWorkflow.CanSubmit(claim, null).IsAllowed);
        }

        [Fact]
        public void All_broken_rules_are_reported_at_once_not_just_the_first()
        {
            var claim = TestData.Claim(TestData.Employee(), title: "");
            claim.Lines.Add(TestData.Line(TestData.Meals(), 0m, daysAgo: -3));

            var errors = ClaimWorkflow.CanSubmit(claim, _policy).Errors.ToList();

            // no title + zero amount + future date
            Assert.True(errors.Count >= 3, "expected several errors, got: " + string.Join(" | ", errors));
        }
    }
}
