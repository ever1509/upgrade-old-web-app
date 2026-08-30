using ExpenseFlow.Domain.Enums;
using ExpenseFlow.Domain.Workflow;
using ExpenseFlow.Tests.Support;
using Xunit;

namespace ExpenseFlow.Tests.Rules
{
    /// <summary>
    /// Visibility and edit permissions. Worth pinning because they are
    /// currently enforced through the ambient CurrentUser static, and the
    /// migration replaces that with injected context - a change with real
    /// potential to leak data between users if it goes wrong.
    /// </summary>
    public class AccessRulesTests
    {
        private const int ManagerId = 2;

        [Fact]
        public void An_owner_can_edit_their_own_draft()
        {
            var alice = TestData.Employee(1, ManagerId);

            Assert.True(ClaimWorkflow.CanEdit(TestData.Claim(alice), alice).IsAllowed);
        }

        [Fact]
        public void A_peer_cannot_edit_someone_elses_claim()
        {
            var alice = TestData.Employee(1, ManagerId);
            var carla = TestData.Employee(3, ManagerId);

            Assert.False(ClaimWorkflow.CanEdit(TestData.Claim(alice), carla).IsAllowed);
        }

        [Fact]
        public void An_admin_can_edit_anyones_claim()
        {
            var alice = TestData.Employee(1, ManagerId);

            Assert.True(ClaimWorkflow.CanEdit(TestData.Claim(alice), TestData.Admin()).IsAllowed);
        }

        [Theory]
        [InlineData(ClaimStatus.Draft, true)]
        [InlineData(ClaimStatus.Rejected, true)]
        [InlineData(ClaimStatus.Submitted, false)]
        [InlineData(ClaimStatus.Approved, false)]
        [InlineData(ClaimStatus.Reimbursed, false)]
        public void A_claim_is_locked_once_it_leaves_the_employees_hands(ClaimStatus status, bool editable)
        {
            var alice = TestData.Employee(1, ManagerId);

            Assert.Equal(editable, ClaimWorkflow.CanEdit(TestData.Claim(alice, status), alice).IsAllowed);
        }

        // ---------- visibility ----------

        [Fact]
        public void An_owner_can_view_their_own_claim()
        {
            var alice = TestData.Employee(1, ManagerId);

            Assert.True(ClaimWorkflow.CanView(TestData.Claim(alice), alice).IsAllowed);
        }

        [Fact]
        public void The_claimants_manager_can_view_it()
        {
            var alice = TestData.Employee(1, ManagerId);

            Assert.True(ClaimWorkflow.CanView(TestData.Claim(alice), TestData.Approver(ManagerId)).IsAllowed);
        }

        [Fact]
        public void An_admin_can_view_it()
        {
            var alice = TestData.Employee(1, ManagerId);

            Assert.True(ClaimWorkflow.CanView(TestData.Claim(alice), TestData.Admin()).IsAllowed);
        }

        [Fact]
        public void An_unrelated_colleague_cannot_view_it()
        {
            var alice = TestData.Employee(1, ManagerId);
            var stranger = TestData.Person(9, "Employee", managerId: 77);

            var result = ClaimWorkflow.CanView(TestData.Claim(alice), stranger);

            Assert.False(result.IsAllowed);
            Assert.Contains("permission", result.FirstError);
        }

        [Fact]
        public void An_approver_from_another_team_cannot_view_it()
        {
            var alice = TestData.Employee(1, ManagerId);
            var otherManager = TestData.Person(8, "Approver", managerId: 4);

            Assert.False(ClaimWorkflow.CanView(TestData.Claim(alice), otherManager).IsAllowed);
        }

        [Fact]
        public void An_anonymous_caller_can_neither_view_nor_edit()
        {
            var claim = TestData.Claim(TestData.Employee(1, ManagerId));

            Assert.False(ClaimWorkflow.CanView(claim, null).IsAllowed);
            Assert.False(ClaimWorkflow.CanEdit(claim, null).IsAllowed);
        }
    }
}
