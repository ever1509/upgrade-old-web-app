using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace ExpenseFlow.Worker.Core.Handlers;

/// <summary>
/// Posts to the .NET Framework web app, which then broadcasts on its SignalR 2
/// hub. Unchanged in intent from the original, but now a typed HttpClient
/// resolved from DI rather than a static field.
///
/// This whole class is temporary. At slice 5, when SignalR moves to ASP.NET
/// Core, the worker can hold IHubContext directly and both this class and the
/// internal endpoint it calls are deleted. Its existence is a measure of how
/// much of the old app is still standing.
/// </summary>
public sealed class NotificationPusher
{
    private readonly HttpClient _http;
    private readonly ILogger<NotificationPusher> _log;
    private readonly WorkerOptions _options;

    public NotificationPusher(HttpClient http, ILogger<NotificationPusher> log, IOptions<WorkerOptions> options)
    {
        _http = http;
        _log = log;
        _options = options.Value;
    }

    public async Task PushAsync(string? email, string title, string message, string level,
                                CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/internal/notify");
            request.Headers.Add("X-ExpenseFlow-Key", _options.InternalApiKey);
            request.Content = JsonContent.Create(new { email, title, message, level });

            var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                _log.LogWarning("Notify endpoint returned {StatusCode} for {Email}",
                    (int)response.StatusCode, email);
        }
        catch (Exception ex)
        {
            // A live toast is a nicety. Never fail the message for it.
            _log.LogWarning(ex, "Could not push a notification to {Email} (is the web app running?)", email);
        }
    }
}
