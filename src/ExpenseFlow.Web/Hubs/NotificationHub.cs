using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;

namespace ExpenseFlow.Web.Hubs
{
    /// <summary>
    /// SignalR 2.x hub. Each signed-in user joins a group named after their
    /// email so the worker can target one person.
    ///
    /// Migration notes for phase 5:
    ///  - Microsoft.AspNet.SignalR -> Microsoft.AspNetCore.SignalR
    ///  - the jQuery-based JS client is replaced by @microsoft/signalr
    ///  - the generated /signalr/hubs proxy no longer exists
    ///  - GlobalHost.ConnectionManager (see below) becomes injected IHubContext
    /// </summary>
    [HubName("notifications")]
    public class NotificationHub : Hub
    {
        public override Task OnConnected()
        {
            var user = Context.User;
            if (user != null && user.Identity.IsAuthenticated)
                Groups.Add(Context.ConnectionId, GroupFor(user.Identity.Name));

            return base.OnConnected();
        }

        public override Task OnReconnected()
        {
            var user = Context.User;
            if (user != null && user.Identity.IsAuthenticated)
                Groups.Add(Context.ConnectionId, GroupFor(user.Identity.Name));

            return base.OnReconnected();
        }

        public static string GroupFor(string email)
        {
            return "user:" + (email ?? string.Empty).ToLowerInvariant();
        }

        /// <summary>
        /// Static global lookup - the SignalR equivalent of HttpContext.Current.
        /// Called from the Web API controller the worker posts to.
        /// </summary>
        public static void PushToUser(string email, string title, string message, string level)
        {
            var context = GlobalHost.ConnectionManager.GetHubContext<NotificationHub>();
            context.Clients.Group(GroupFor(email)).notify(new
            {
                title = title,
                message = message,
                level = level
            });
        }
    }
}
