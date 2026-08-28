using System;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.Security;
using System.Security.Principal;
using System.Threading;
using log4net;
using log4net.Config;

namespace ExpenseFlow.Web
{
    /// <summary>
    /// The classic composition root. Application_Start, per-request events,
    /// and a global error handler all hang off this one class.
    ///
    /// In ASP.NET Core every one of these events becomes either middleware
    /// or a hosted service, so this file is the single best illustration of
    /// what the migration actually changes.
    /// </summary>
    public class MvcApplication : HttpApplication
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(MvcApplication));

        protected void Application_Start()
        {
            XmlConfigurator.Configure();

            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            Log.Info("ExpenseFlow started.");
        }

        /// <summary>
        /// Rebuilds the role information on every request from the Forms auth
        /// ticket's UserData field. Hand-rolled because the app never adopted
        /// the ASP.NET role provider.
        /// </summary>
        protected void Application_PostAuthenticateRequest(object sender, EventArgs e)
        {
            var cookie = Request.Cookies[FormsAuthentication.FormsCookieName];
            if (cookie == null) return;

            FormsAuthenticationTicket ticket;
            try
            {
                ticket = FormsAuthentication.Decrypt(cookie.Value);
            }
            catch (Exception ex)
            {
                Log.Warn("Could not decrypt the auth cookie; signing out.", ex);
                FormsAuthentication.SignOut();
                return;
            }

            if (ticket == null || ticket.Expired) return;

            // UserData holds "employeeId|Role".
            var parts = (ticket.UserData ?? string.Empty).Split('|');
            var roles = parts.Length > 1 ? new[] { parts[1] } : new string[0];

            var identity = new FormsIdentity(ticket);
            var principal = new GenericPrincipal(identity, roles);

            HttpContext.Current.User = principal;
            Thread.CurrentPrincipal = principal;
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            var ex = Server.GetLastError();
            if (ex != null) Log.Error("Unhandled application error.", ex);
        }

        protected void Application_End()
        {
            Log.Info("ExpenseFlow stopping.");
        }
    }
}
