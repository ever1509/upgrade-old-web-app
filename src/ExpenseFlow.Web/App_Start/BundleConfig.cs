using System.Web.Optimization;

namespace ExpenseFlow.Web
{
    /// <summary>
    /// System.Web.Optimization bundling. It does not exist in ASP.NET Core -
    /// you move to a real front-end build (Vite/esbuild) or to the
    /// static-asset pipeline. A small item that is universally forgotten
    /// until the CSS stops loading in the migrated app.
    ///
    /// Only CSS is bundled here; the two scripts this app needs come from a
    /// CDN so that a plain "nuget restore" is enough to run the solution.
    /// </summary>
    public class BundleConfig
    {
        public static void RegisterBundles(BundleCollection bundles)
        {
            bundles.Add(new StyleBundle("~/Content/css").Include("~/Content/site.css"));

            // Off in development so the browser shows the real file.
            BundleTable.EnableOptimizations = false;
        }
    }
}
