using System;
using System.Web.Mvc;
using ExpenseFlow.Web.Models;

namespace ExpenseFlow.Web.Controllers
{
    /// <summary>
    /// Read-only reporting over stored procedures. Because it never writes
    /// and has a small surface, this controller is the LOWEST-RISK slice to
    /// move across during the strangler phase - which is why the plan
    /// migrates it second, right after the worker.
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class AdminController : BaseController
    {
        public ActionResult Reports(DateTime? from, DateTime? to)
        {
            var model = new ReportsViewModel();
            if (from.HasValue) model.FromUtc = from.Value;
            if (to.HasValue) model.ToUtc = to.Value;

            model.ByDepartment = Reports.SpendByDepartment(model.FromUtc, model.ToUtc);
            model.ByCategory = Reports.SpendByCategory(model.FromUtc, model.ToUtc);

            return View(model);
        }
    }
}
