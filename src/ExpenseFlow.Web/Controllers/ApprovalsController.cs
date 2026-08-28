using System;
using System.Web.Mvc;
using ExpenseFlow.Domain.Workflow;
using ExpenseFlow.Messaging.Contracts;
using ExpenseFlow.Web.Models;
using log4net;

namespace ExpenseFlow.Web.Controllers
{
    public class ApprovalsController : BaseController
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ApprovalsController));

        public ActionResult Index()
        {
            var me = Me;
            if (me == null) return RedirectToAction("Login", "Account");
            if (!me.CanApprove) return Denied("You do not have permission to review claims.");

            return View(new ClaimListViewModel
            {
                Claims = Claims.GetAwaitingDecisionFor(me),
                Heading = "Awaiting your decision",
                ShowClaimant = true
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Approve(DecisionViewModel model)
        {
            return Decide(model, approved: true);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Reject(DecisionViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Comment))
            {
                TempData["Error"] = "A rejection needs a reason.";
                return RedirectToAction("Details", "Claims", new { id = model.ClaimId });
            }
            return Decide(model, approved: false);
        }

        private ActionResult Decide(DecisionViewModel model, bool approved)
        {
            var claim = Claims.GetByIdWithDetails(model.ClaimId);
            var me = Me;

            var check = ClaimWorkflow.CanDecide(claim, me, AppSettings.Policy);
            if (!check.IsAllowed) return Denied(check.FirstError);

            if (approved) ClaimWorkflow.Approve(claim, me, model.Comment);
            else ClaimWorkflow.Reject(claim, me, model.Comment);

            Claims.Save();

            try
            {
                Publisher.Publish(ClaimDecidedMessage.Type, new ClaimDecidedMessage
                {
                    ClaimId = claim.Id,
                    ClaimNumber = claim.ClaimNumber,
                    EmployeeId = claim.EmployeeId,
                    EmployeeEmail = claim.Employee == null ? null : claim.Employee.Email,
                    EmployeeName = claim.Employee == null ? null : claim.Employee.FullName,
                    Approved = approved,
                    DecidedByName = me.FullName,
                    Reason = model.Comment,
                    TotalAmount = claim.TotalAmount
                }, claim.ClaimNumber);
            }
            catch (Exception ex)
            {
                Log.Error("Could not publish claim.decided.", ex);
                TempData["Warning"] = "Decision saved, but the notification service could not be reached.";
            }

            TempData["Success"] = "Claim " + claim.ClaimNumber + (approved ? " approved." : " rejected.");
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Reimburse(int id)
        {
            var claim = Claims.GetByIdWithDetails(id);
            var me = Me;

            var check = ClaimWorkflow.CanReimburse(claim, me);
            if (!check.IsAllowed) return Denied(check.FirstError);

            ClaimWorkflow.Reimburse(claim, me);
            Claims.Save();

            TempData["Success"] = "Claim " + claim.ClaimNumber + " marked reimbursed.";
            return RedirectToAction("Details", "Claims", new { id = id });
        }
    }
}
