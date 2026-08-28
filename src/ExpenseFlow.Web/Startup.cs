using Microsoft.Owin;
using Owin;

[assembly: OwinStartup(typeof(ExpenseFlow.Web.Startup))]

namespace ExpenseFlow.Web
{
    /// <summary>
    /// OWIN startup, present only so SignalR 2 can map its hubs. The app
    /// still authenticates through classic Forms Auth in system.web, so
    /// this pipeline and the ASP.NET pipeline coexist awkwardly - a very
    /// common state for apps of this vintage.
    /// </summary>
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.MapSignalR();
        }
    }
}
