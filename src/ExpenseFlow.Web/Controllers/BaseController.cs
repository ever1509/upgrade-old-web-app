using System.Web.Mvc;
using ExpenseFlow.Data;
using ExpenseFlow.Data.Repositories;
using ExpenseFlow.Domain.Entities;
using ExpenseFlow.Messaging;
using ExpenseFlow.Web.Security;

namespace ExpenseFlow.Web.Controllers
{
    /// <summary>
    /// Poor man's dependency injection: the base controller news up its own
    /// DbContext, repositories and publisher, and disposes them itself.
    ///
    /// No container, no interfaces at the composition root, no way to
    /// substitute a fake in a test. This is the single biggest reason the
    /// controllers in this app are hard to unit test - and the reason the
    /// characterization tests in phase 2 will target ClaimWorkflow (which is
    /// pure) rather than the controllers.
    /// </summary>
    public abstract class BaseController : Controller
    {
        private ExpenseFlowContext _db;
        private IMessagePublisher _publisher;

        protected ExpenseFlowContext Db
        {
            get { return _db ?? (_db = new ExpenseFlowContext()); }
        }

        protected IClaimRepository Claims
        {
            get { return new ClaimRepository(Db); }
        }

        protected IEmployeeRepository Employees
        {
            get { return new EmployeeRepository(Db); }
        }

        protected ILookupRepository Lookups
        {
            get { return new LookupRepository(Db); }
        }

        /// <summary>
        /// Named "Reporting" rather than "Reports" so it does not collide with
        /// AdminController's Reports action - a method name always wins over an
        /// inherited property, which is the kind of trap a base class full of
        /// convenience members sets for you.
        /// </summary>
        protected IReportRepository Reporting
        {
            get { return new ReportRepository(Db); }
        }

        protected IMessagePublisher Publisher
        {
            get
            {
                return _publisher ?? (_publisher = MessagingFactory.CreatePublisher(
                    AppSettings.Transport, AppSettings.QueuePath, AppSettings.QueueDirectory));
            }
        }

        /// <summary>The signed-in employee, loaded through the static ambient accessor.</summary>
        protected Employee Me
        {
            get { return CurrentUser.Employee; }
        }

        protected ActionResult Denied(string reason)
        {
            TempData["Error"] = reason;
            return RedirectToAction("Index", "Home");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_db != null) { _db.Dispose(); _db = null; }

                var disposablePublisher = _publisher as System.IDisposable;
                if (disposablePublisher != null) { disposablePublisher.Dispose(); _publisher = null; }
            }
            base.Dispose(disposing);
        }
    }
}
