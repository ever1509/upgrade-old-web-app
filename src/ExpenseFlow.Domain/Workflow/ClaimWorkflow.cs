using System;
using System.Linq;
using ExpenseFlow.Domain.Entities;
using ExpenseFlow.Domain.Enums;

namespace ExpenseFlow.Domain.Workflow
{
    /// <summary>
    /// All expense-claim business rules, deliberately kept free of
    /// System.Web, EF and configuration so it is pure, testable logic.
    ///
    /// This class is the anchor of the whole migration: it is what the
    /// characterization tests in phase 2 pin down, and because it has no
    /// framework dependencies it is the first thing that will compile
    /// unchanged on .NET 10. Everything painful in this solution is painful
    /// precisely because it is NOT written like this.
    /// </summary>
    public static class ClaimWorkflow
    {
        // ---------- submit ----------

        public static RuleResult CanSubmit(ExpenseClaim claim, ApprovalPolicy policy)
        {
            if (claim == null) return RuleResult.Deny("Claim not found.");
            if (policy == null) policy = new ApprovalPolicy();

            var result = RuleResult.Allow();

            result.And(claim.StatusValue == ClaimStatus.Draft || claim.StatusValue == ClaimStatus.Rejected,
                       "Only draft or rejected claims can be submitted.");

            result.And(!string.IsNullOrWhiteSpace(claim.Title),
                       "A claim must have a title.");

            var lines = claim.Lines == null ? new ExpenseLine[0] : claim.Lines.ToArray();

            result.And(lines.Length > 0,
                       "A claim must have at least one expense line.");

            result.And(lines.Length <= policy.MaxLinesPerClaim,
                       string.Format("A claim cannot have more than {0} lines.", policy.MaxLinesPerClaim));

            result.And(lines.All(l => l.Amount > 0m),
                       "Every expense line must have an amount greater than zero.");

            result.And(lines.All(l => l.ExpenseDate <= DateTime.UtcNow.Date.AddDays(1)),
                       "Expense lines cannot be dated in the future.");

            var total = lines.Sum(l => l.Amount);
            result.And(total <= policy.MaxClaimAmount,
                       string.Format("A claim cannot exceed {0:N2}. This one totals {1:N2}.",
                                     policy.MaxClaimAmount, total));

            foreach (var line in lines)
            {
                if (line.Category == null) continue;
                if (!line.Category.RequiresReceipt) continue;
                if (line.Amount <= line.Category.MaxAmountWithoutReceipt) continue;

                result.And(line.HasReceipt,
                           string.Format("'{0}' ({1:N2}) needs a receipt: {2} allows at most {3:N2} without one.",
                                         line.Description, line.Amount,
                                         line.Category.Name, line.Category.MaxAmountWithoutReceipt));
            }

            return result;
        }

        public static void Submit(ExpenseClaim claim, Employee actor)
        {
            var wasRejected = claim.StatusValue == ClaimStatus.Rejected;

            claim.StatusValue = ClaimStatus.Submitted;
            claim.SubmittedUtc = DateTime.UtcNow;
            claim.TotalAmount = claim.CalculateTotal();
            claim.RejectionReason = null;
            claim.DecidedUtc = null;
            claim.DecidedByEmployeeId = null;

            AddHistory(claim, wasRejected ? ApprovalAction.Resubmitted : ApprovalAction.Submitted, actor, null);
        }

        // ---------- decide ----------

        public static RuleResult CanDecide(ExpenseClaim claim, Employee approver, ApprovalPolicy policy)
        {
            if (claim == null) return RuleResult.Deny("Claim not found.");
            if (approver == null) return RuleResult.Deny("You must be signed in to decide a claim.");
            if (policy == null) policy = new ApprovalPolicy();

            var result = RuleResult.Allow();

            result.And(claim.StatusValue == ClaimStatus.Submitted,
                       "Only submitted claims can be approved or rejected.");

            result.And(claim.EmployeeId != approver.Id,
                       "You cannot decide your own claim.");

            result.And(approver.CanApprove,
                       "You do not have permission to decide claims.");

            // The approver must be the claimant's manager, or any Admin.
            var isManagerOfClaimant = claim.Employee != null && claim.Employee.ManagerId == approver.Id;
            result.And(isManagerOfClaimant || approver.IsAdmin,
                       "You can only decide claims for your own direct reports.");

            // Large claims escalate: only an Admin may decide them.
            if (claim.TotalAmount >= policy.SeniorApprovalThreshold)
            {
                result.And(approver.IsAdmin,
                           string.Format("Claims of {0:N2} or more must be decided by Finance (Admin role).",
                                         policy.SeniorApprovalThreshold));
            }

            return result;
        }

        public static void Approve(ExpenseClaim claim, Employee approver, string comment)
        {
            claim.StatusValue = ClaimStatus.Approved;
            claim.DecidedUtc = DateTime.UtcNow;
            claim.DecidedByEmployeeId = approver.Id;
            claim.RejectionReason = null;

            AddHistory(claim, ApprovalAction.Approved, approver, comment);
        }

        public static void Reject(ExpenseClaim claim, Employee approver, string reason)
        {
            claim.StatusValue = ClaimStatus.Rejected;
            claim.DecidedUtc = DateTime.UtcNow;
            claim.DecidedByEmployeeId = approver.Id;
            claim.RejectionReason = reason;

            AddHistory(claim, ApprovalAction.Rejected, approver, reason);
        }

        // ---------- reimburse ----------

        public static RuleResult CanReimburse(ExpenseClaim claim, Employee actor)
        {
            if (claim == null) return RuleResult.Deny("Claim not found.");
            if (actor == null) return RuleResult.Deny("You must be signed in.");

            return RuleResult.Allow()
                .And(claim.StatusValue == ClaimStatus.Approved, "Only approved claims can be marked reimbursed.")
                .And(actor.IsAdmin, "Only Finance (Admin role) can mark a claim reimbursed.");
        }

        public static void Reimburse(ExpenseClaim claim, Employee actor)
        {
            claim.StatusValue = ClaimStatus.Reimbursed;
            AddHistory(claim, ApprovalAction.Reimbursed, actor, null);
        }

        // ---------- edit ----------

        public static RuleResult CanEdit(ExpenseClaim claim, Employee actor)
        {
            if (claim == null) return RuleResult.Deny("Claim not found.");
            if (actor == null) return RuleResult.Deny("You must be signed in.");

            return RuleResult.Allow()
                .And(claim.IsEditable, "This claim is locked because it has already been submitted.")
                .And(claim.EmployeeId == actor.Id || actor.IsAdmin, "You can only edit your own claims.");
        }

        public static RuleResult CanView(ExpenseClaim claim, Employee actor)
        {
            if (claim == null) return RuleResult.Deny("Claim not found.");
            if (actor == null) return RuleResult.Deny("You must be signed in.");

            var isOwner = claim.EmployeeId == actor.Id;
            var isClaimantsManager = claim.Employee != null && claim.Employee.ManagerId == actor.Id;

            return RuleResult.Allow()
                .And(isOwner || isClaimantsManager || actor.IsAdmin,
                     "You do not have permission to view this claim.");
        }

        private static void AddHistory(ExpenseClaim claim, ApprovalAction action, Employee actor, string comment)
        {
            claim.History.Add(new ApprovalHistory
            {
                ClaimId = claim.Id,
                Action = action.ToString(),
                ActorEmployeeId = actor.Id,
                Comment = comment,
                OccurredUtc = DateTime.UtcNow
            });
        }
    }
}
