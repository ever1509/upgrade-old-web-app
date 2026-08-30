using ExpenseFlow.Domain.Enums;
using ExpenseFlow.Domain.Workflow;
using ExpenseFlow.Tests.Support;
using Xunit;

namespace ExpenseFlow.Tests.Rules
{
    /// <summary>
    /// Who is allowed to approve or reject a claim. These are the rules most
    /// likely to be quietly broken by an auth migration, so they are the ones
    /// worth pinning hardest.
    /// </summary>
    public class DecisionRulesTests
    {
        private readonly ApprovalPolicy _policy = new ApprovalPolicy();

        private static readonly int ManagerId = 2;
        private static readonly int AdminId = 4;

        [Fact]
        public void The_claimants_manager_can_decide()
        {
            var claim = SubmittedClaim(100m);

            Assert.True(ClaimWorkflow.CanDecide(claim, TestData.Approver(ManagerId), _policy).IsAllowed);
        }

        [Fact]
        public void An_admin_can_decide_anyones_claim()
        {
            var claim = SubmittedClaim(100m);

            Assert.True(ClaimWorkflow.CanDecide(claim, TestData.Admin(AdminId), _policy).IsAllowed);
        }

        [Fact]
        public void You_cannot_decide_your_own_claim()
        {
            var approver = TestData.Approver(ManagerId);
            var claim = TestData.Claim(approver, ClaimStatus.Submitted);
            claim.TotalAmount = 100m;

            var result = ClaimWorkflow.CanDecide(claim, approver, _policy);

            Assert.False(result.IsAllowed);
            Assert.Contains(result.Errors, e => e.Contains("your own claim"));
        }

        [Fact]
        public void Not_even_an_admin_can_decide_their_own_claim()
        {
            var admin = TestData.Admin(AdminId);
            var claim = TestData.Claim(admin, ClaimStatus.Submitted);
            claim.TotalAmount = 100m;

            Assert.False(ClaimWorkflow.CanDecide(claim, admin, _policy).IsAllowed);
        }

        [Fact]
        public void An_approver_cannot_decide_for_someone_who_is_not_their_report()
        {
            var claim = SubmittedClaim(100m);
            claim.Employee.ManagerId = 99;   // reports to somebody else

            var result = ClaimWorkflow.CanDecide(claim, TestData.Approver(ManagerId), _policy);

            Assert.False(result.IsAllowed);
            Assert.Contains(result.Errors, e => e.Contains("direct reports"));
        }

        [Fact]
        public void A_plain_employee_cannot_decide_at_all()
        {
            var claim = SubmittedClaim(100m);

            Assert.False(ClaimWorkflow.CanDecide(claim, TestData.Person(7, "Employee", ManagerId), _policy).IsAllowed);
        }

        // ---------- the senior-approval threshold ----------

        [Theory]
        [InlineData(100.00, true)]
        [InlineData(499.99, true)]
        [InlineData(500.00, false)]   // at the threshold it already escalates
        [InlineData(750.00, false)]
        public void Large_claims_escalate_past_the_line_manager(decimal total, bool managerMayDecide)
        {
            var claim = SubmittedClaim(total);

            Assert.Equal(managerMayDecide,
                ClaimWorkflow.CanDecide(claim, TestData.Approver(ManagerId), _policy).IsAllowed);
        }

        [Fact]
        public void An_admin_can_always_decide_a_large_claim()
        {
            var claim = SubmittedClaim(5000m);

            Assert.True(ClaimWorkflow.CanDecide(claim, TestData.Admin(AdminId), _policy).IsAllowed);
        }

        [Fact]
        public void The_escalation_error_points_at_finance()
        {
            var claim = SubmittedClaim(750m);

            var error = ClaimWorkflow.CanDecide(claim, TestData.Approver(ManagerId), _policy).FirstError;

            Assert.Contains("Finance", error);
            Assert.Contains("500.00", error);
        }

        // ---------- status gating ----------

        [Theory]
        [InlineData(ClaimStatus.Draft)]
        [InlineData(ClaimStatus.Approved)]
        [InlineData(ClaimStatus.Rejected)]
        [InlineData(ClaimStatus.Reimbursed)]
        public void Only_a_submitted_claim_can_be_decided(ClaimStatus status)
        {
            var claim = TestData.Claim(TestData.Employee(1, ManagerId), status);
            claim.TotalAmount = 100m;

            Assert.False(ClaimWorkflow.CanDecide(claim, TestData.Approver(ManagerId), _policy).IsAllowed);
        }

        [Fact]
        public void A_null_approver_is_denied_rather_than_throwing()
        {
            Assert.False(ClaimWorkflow.CanDecide(SubmittedClaim(10m), null, _policy).IsAllowed);
        }

        [Fact]
        public void A_null_claim_is_denied_rather_than_throwing()
        {
            Assert.False(ClaimWorkflow.CanDecide(null, TestData.Admin(AdminId), _policy).IsAllowed);
        }

        private static Domain.Entities.ExpenseClaim SubmittedClaim(decimal total)
        {
            var claim = TestData.Claim(TestData.Employee(1, ManagerId), ClaimStatus.Submitted);
            claim.TotalAmount = total;
            return claim;
        }
    }
}
