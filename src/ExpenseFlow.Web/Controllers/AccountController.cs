using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using ExpenseFlow.Web.Models;
using ExpenseFlow.Web.Security;
using log4net;

namespace ExpenseFlow.Web.Controllers
{
    /// <summary>
    /// Classic Forms Authentication: verify the password, encrypt a ticket,
    /// drop a cookie. FormsAuthentication is System.Web-only, so this whole
    /// controller is rewritten during the migration - and because auth
    /// touches every request, it is scheduled LAST.
    /// </summary>
    [AllowAnonymous]
    public class AccountController : BaseController
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(AccountController));

        [HttpGet]
        public ActionResult Login(string returnUrl)
        {
            if (CurrentUser.IsAuthenticated) return RedirectToAction("Index", "Home");
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var employee = Employees.GetByEmail(model.Email);

            if (employee == null || !PasswordHasher.Verify(model.Password, employee.PasswordSalt, employee.PasswordHash))
            {
                // Deliberately vague, so the form does not confirm which emails exist.
                ModelState.AddModelError(string.Empty, "That email and password combination was not recognised.");
                Log.WarnFormat("Failed sign-in attempt for {0}", model.Email);
                return View(model);
            }

            IssueAuthCookie(employee.Email, employee.Id, employee.Role, model.RememberMe);
            Log.InfoFormat("{0} signed in.", employee.Email);

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            Session.Abandon();
            return RedirectToAction("Login");
        }

        private void IssueAuthCookie(string email, int employeeId, string role, bool persistent)
        {
            var ticket = new FormsAuthenticationTicket(
                version: 1,
                name: email,
                issueDate: DateTime.Now,
                expiration: DateTime.Now.AddMinutes(FormsAuthentication.Timeout.TotalMinutes),
                isPersistent: persistent,
                userData: employeeId + "|" + role,
                cookiePath: FormsAuthentication.FormsCookiePath);

            var encrypted = FormsAuthentication.Encrypt(ticket);

            var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, encrypted)
            {
                HttpOnly = true,
                Path = FormsAuthentication.FormsCookiePath,
                Secure = FormsAuthentication.RequireSSL
            };

            if (persistent) cookie.Expires = ticket.Expiration;

            Response.Cookies.Add(cookie);
        }
    }
}
