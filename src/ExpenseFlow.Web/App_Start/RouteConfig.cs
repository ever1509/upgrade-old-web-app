using System.Web.Mvc;
using System.Web.Routing;

namespace ExpenseFlow.Web
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            // Leave the System.Web adapters' endpoints alone.
            //
            // "/systemweb-adapters/authenticate" matches the default
            // {controller}/{action}/{id} route exactly, so MVC claims the
            // request, finds no such controller, and returns its own 404 -
            // and the adapters never see it. The ASP.NET Core app then gets
            // nothing back and treats every user as anonymous, with no error
            // anywhere to suggest routing was the cause.
            routes.IgnoreRoute("systemweb-adapters/{*pathInfo}");

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
