using System;
using System.Net.Http;
using System.Text;
using log4net;
using Newtonsoft.Json;

namespace ExpenseFlow.Worker.Handlers
{
    /// <summary>
    /// The service cannot broadcast on the SignalR hub itself - hubs live in
    /// the web app's process - so it posts to an internal endpoint with a
    /// shared key and lets the web app do the push.
    ///
    /// After the migration this hop disappears: the worker can take an
    /// IHubContext&lt;NotificationHub&gt; directly, or both processes share a
    /// SignalR backplane.
    /// </summary>
    public class NotificationPusher
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(NotificationPusher));
        private static readonly HttpClient Http = new HttpClient();

        private readonly string _baseUrl;
        private readonly string _apiKey;

        public NotificationPusher(string baseUrl, string apiKey)
        {
            _baseUrl = (baseUrl ?? string.Empty).TrimEnd('/') + "/";
            _apiKey = apiKey;
        }

        public void Push(string email, string title, string message, string level)
        {
            if (string.IsNullOrWhiteSpace(email)) return;

            try
            {
                var payload = JsonConvert.SerializeObject(new
                {
                    email = email,
                    title = title,
                    message = message,
                    level = level
                });

                using (var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl + "api/internal/notify"))
                {
                    request.Headers.Add("X-ExpenseFlow-Key", _apiKey);
                    request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                    var response = Http.SendAsync(request).GetAwaiter().GetResult();
                    if (!response.IsSuccessStatusCode)
                        Log.WarnFormat("Notify endpoint returned {0} for {1}", (int)response.StatusCode, email);
                }
            }
            catch (Exception ex)
            {
                // A live toast is a nicety. Never fail the message for it.
                Log.Warn("Could not push a live notification to " + email + " (is the web app running?)", ex);
            }
        }
    }
}
