using System.Web.Http;
using ExpenseFlow.Web.Hubs;

namespace ExpenseFlow.Web.Controllers.Api
{
    public class NotifyRequest
    {
        public string Email { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string Level { get; set; }
    }

    /// <summary>
    /// The Windows Service cannot reach the SignalR hub directly - hubs live
    /// inside the web app's process - so it posts here with a shared secret
    /// and the web app does the broadcast.
    ///
    /// A shared key in web.config, compared with ==. Both the key handling
    /// and the process-boundary hop are things the migration cleans up:
    /// in ASP.NET Core the worker can hold IHubContext directly, or you use
    /// a SignalR backplane.
    /// </summary>
    [AllowAnonymous]
    [RoutePrefix("api/internal")]
    public class InternalNotificationsController : ApiController
    {
        [HttpPost]
        [Route("notify")]
        public IHttpActionResult Notify(NotifyRequest request)
        {
            var provided = Request.Headers.Contains("X-ExpenseFlow-Key")
                ? System.Linq.Enumerable.FirstOrDefault(Request.Headers.GetValues("X-ExpenseFlow-Key"))
                : null;

            if (provided != AppSettings.InternalApiKey) return Unauthorized();
            if (request == null || string.IsNullOrWhiteSpace(request.Email)) return BadRequest("Email is required.");

            NotificationHub.PushToUser(request.Email, request.Title, request.Message, request.Level ?? "info");

            return Ok(new { delivered = true });
        }
    }
}
