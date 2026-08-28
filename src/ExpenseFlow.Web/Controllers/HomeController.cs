using System.Linq;
using System.Web.Mvc;
using ExpenseFlow.Domain.Enums;

namespace ExpenseFlow.Web.Controllers
{
    public class HomeController : BaseController
    {
        public ActionResult Index()
        {
            var me = Me;
            if (me == null) return RedirectToAction("Login", "Account");

            var mine = Claims.GetForEmployee(me.Id);

            ViewBag.DraftCount = mine.Count(c => c.StatusValue == ClaimStatus.Draft);
            ViewBag.SubmittedCount = mine.Count(c => c.StatusValue == ClaimStatus.Submitted);
            ViewBag.ApprovedCount = mine.Count(c => c.StatusValue == ClaimStatus.Approved);
            ViewBag.RejectedCount = mine.Count(c => c.StatusValue == ClaimStatus.Rejected);
            ViewBag.TotalApproved = mine.Where(c => c.StatusValue == ClaimStatus.Approved ||
                                                    c.StatusValue == ClaimStatus.Reimbursed)
                                        .Sum(c => (decimal?)c.TotalAmount) ?? 0m;

            ViewBag.AwaitingMyDecision = me.CanApprove ? Claims.GetAwaitingDecisionFor(me).Count : 0;

            return View();
        }
    }
}
