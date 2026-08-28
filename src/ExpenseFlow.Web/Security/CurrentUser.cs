using System.Web;
using ExpenseFlow.Data;
using ExpenseFlow.Data.Repositories;
using ExpenseFlow.Domain.Entities;

namespace ExpenseFlow.Web.Security
{
    /// <summary>
    /// *** THE ARCHETYPAL BLOCKER ***
    ///
    /// A static class reaching into HttpContext.Current from anywhere -
    /// controllers, views, even domain-ish helpers. It is ambient state
    /// disguised as a convenience.
    ///
    /// HttpContext.Current does not exist in ASP.NET Core. Every call site
    /// has to become an injected IHttpContextAccessor or, better, an
    /// explicit parameter. Grep for "CurrentUser." to see the blast radius -
    /// that grep is a genuinely useful assessment exercise.
    /// </summary>
    public static class CurrentUser
    {
        private const string CacheKey = "ExpenseFlow.CurrentUser";

        public static bool IsAuthenticated
        {
            get
            {
                var ctx = HttpContext.Current;
                return ctx != null && ctx.User != null && ctx.User.Identity.IsAuthenticated;
            }
        }

        public static string Email
        {
            get { return IsAuthenticated ? HttpContext.Current.User.Identity.Name : null; }
        }

        /// <summary>
        /// Loads the employee once per request and stashes it in
        /// HttpContext.Items. Opens its own DbContext, which is a second
        /// connection on top of whatever the controller already has.
        /// </summary>
        public static Employee Employee
        {
            get
            {
                var ctx = HttpContext.Current;
                if (ctx == null || !IsAuthenticated) return null;

                if (ctx.Items[CacheKey] != null) return (Employee)ctx.Items[CacheKey];

                using (var db = new ExpenseFlowContext())
                {
                    var repo = new EmployeeRepository(db);
                    var employee = repo.GetByEmail(ctx.User.Identity.Name);

                    // Detach so the caller can use it after the context is gone.
                    if (employee != null) db.Entry(employee).State = System.Data.Entity.EntityState.Detached;

                    ctx.Items[CacheKey] = employee;
                    return employee;
                }
            }
        }

        public static bool IsInRole(string role)
        {
            return IsAuthenticated && HttpContext.Current.User.IsInRole(role);
        }

        public static bool CanApprove
        {
            get { return IsInRole("Approver") || IsInRole("Admin"); }
        }

        public static bool IsAdmin
        {
            get { return IsInRole("Admin"); }
        }
    }
}
