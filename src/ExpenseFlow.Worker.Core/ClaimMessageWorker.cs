using ExpenseFlow.Messaging;
using ExpenseFlow.Messaging.Contracts;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace ExpenseFlow.Worker.Core;

/// <summary>
/// The .NET 10 replacement for the ServiceBase-derived Windows Service
/// (ledger item B2).
///
/// The differences worth noticing:
///   * BackgroundService instead of ServiceBase, so it runs anywhere
///   * constructor injection instead of newing up its own dependencies
///   * ILogger instead of a static log4net field
///   * CancellationToken instead of a volatile bool and a manual Thread
///
/// The message-handling logic itself is nearly unchanged, because it never
/// touched System.Web. That is the whole reason this slice went first.
/// </summary>
public sealed class ClaimMessageWorker : BackgroundService
{
    private readonly ILogger<ClaimMessageWorker> _log;
    private readonly WorkerOptions _options;

    public ClaimMessageWorker(ILogger<ClaimMessageWorker> log, IOptions<WorkerOptions> options)
    {
        _log = log;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("Listening on file queue {QueueDirectory}", _options.QueueDirectory);

        using var receiver = new FileSystemMessageReceiver(_options.QueueDirectory);
        var timeout = TimeSpan.FromSeconds(_options.ReceiveTimeoutSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // The receiver is synchronous and blocking, inherited unchanged
                // from the .NET Framework worker. Pushed off the loop thread so
                // cancellation stays responsive; it becomes properly async when
                // RabbitMQ replaces it at phase 4.
                await Task.Run(() => receiver.TryReceive(timeout, Handle), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Message handling failed; it will be retried or dead-lettered.");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }

        _log.LogInformation("Worker loop stopped.");
    }

    private void Handle(MessageEnvelope envelope)
    {
        _log.LogInformation("Received {MessageType} ({CorrelationId})",
            envelope.MessageType, envelope.CorrelationId);

        switch (envelope.MessageType)
        {
            case ClaimSubmittedMessage.Type:
                var submitted = JsonConvert.DeserializeObject<ClaimSubmittedMessage>(envelope.Payload);
                _log.LogInformation(
                    "  claim {ClaimNumber} from {EmployeeName} for {TotalAmount:N2} — approver {ApproverEmail}",
                    submitted?.ClaimNumber, submitted?.EmployeeName, submitted?.TotalAmount, submitted?.ApproverEmail);
                // TODO slice 1: thumbnails (ImageSharp), PDF (QuestPDF), email (MailKit)
                break;

            case ClaimDecidedMessage.Type:
                var decided = JsonConvert.DeserializeObject<ClaimDecidedMessage>(envelope.Payload);
                _log.LogInformation("  claim {ClaimNumber} was {Verdict} by {DecidedByName}",
                    decided?.ClaimNumber, decided?.Approved == true ? "approved" : "rejected", decided?.DecidedByName);
                break;

            default:
                _log.LogWarning("Unknown message type '{MessageType}'; discarding.", envelope.MessageType);
                break;
        }
    }
}
