using ExpenseFlow.Messaging;
using ExpenseFlow.Messaging.Contracts;
using ExpenseFlow.Worker.Core.Data;
using ExpenseFlow.Worker.Core.Handlers;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace ExpenseFlow.Worker.Core;

/// <summary>
/// The .NET 10 replacement for the ServiceBase-derived Windows Service
/// (ledger item B2), now doing the real work rather than just logging.
///
/// What changed from the .NET Framework original:
///   * BackgroundService instead of ServiceBase, so it runs anywhere
///   * constructor injection instead of newing up its own dependencies
///   * ILogger instead of a static log4net field
///   * CancellationToken instead of a volatile bool and a manual Thread
///   * IClaimStore instead of a DbContext created inline
///
/// What did NOT change: the sequence of work, and the decision to let a failed
/// thumbnail or PDF be logged rather than fail the message. Behaviour is meant
/// to be identical so the output can be diffed against the pre-migration
/// baseline. Anything that differs is a bug, not an improvement.
/// </summary>
public sealed class ClaimMessageWorker : BackgroundService
{
    private readonly ILogger<ClaimMessageWorker> _log;
    private readonly WorkerOptions _options;
    private readonly IClaimStore _claims;
    private readonly EmailSender _email;
    private readonly NotificationPusher _notifier;

    public ClaimMessageWorker(
        ILogger<ClaimMessageWorker> log,
        IOptions<WorkerOptions> options,
        IClaimStore claims,
        EmailSender email,
        NotificationPusher notifier)
    {
        _log = log;
        _options = options.Value;
        _claims = claims;
        _email = email;
        _notifier = notifier;
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
                await Task.Run(() => receiver.TryReceive(timeout, HandleBlocking), stoppingToken);
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

    /// <summary>
    /// The receiver's contract is synchronous: throwing must leave the message
    /// retryable, so the async work is joined here rather than fired and
    /// forgotten. Becomes a genuinely async handler at phase 4.
    /// </summary>
    private void HandleBlocking(MessageEnvelope envelope) =>
        HandleAsync(envelope).GetAwaiter().GetResult();

    private async Task HandleAsync(MessageEnvelope envelope)
    {
        _log.LogInformation("Received {MessageType} ({CorrelationId})",
            envelope.MessageType, envelope.CorrelationId);

        switch (envelope.MessageType)
        {
            case ClaimSubmittedMessage.Type:
                var submitted = JsonConvert.DeserializeObject<ClaimSubmittedMessage>(envelope.Payload);
                if (submitted is not null) await HandleSubmittedAsync(submitted);
                break;

            case ClaimDecidedMessage.Type:
                var decided = JsonConvert.DeserializeObject<ClaimDecidedMessage>(envelope.Payload);
                if (decided is not null) await HandleDecidedAsync(decided);
                break;

            default:
                _log.LogWarning("Unknown message type '{MessageType}'; discarding.", envelope.MessageType);
                break;
        }
    }

    private async Task HandleSubmittedAsync(ClaimSubmittedMessage message)
    {
        var claim = _claims.GetClaimWithDetails(message.ClaimId);
        if (claim is null)
        {
            _log.LogWarning("Claim {ClaimId} no longer exists.", message.ClaimId);
            return;
        }

        // 1. Thumbnails - ImageSharp, was System.Drawing / GDI+
        foreach (var receipt in claim.Lines.SelectMany(l => l.Receipts).ToList())
        {
            if (receipt.HasThumbnail) continue;
            if (!ThumbnailRenderer.IsRenderable(receipt.ContentType, receipt.FileName)) continue;

            try
            {
                var relative = await ThumbnailRenderer.RenderAsync(_options.UploadRoot, receipt.StoredPath);
                _claims.SetReceiptThumbnail(receipt.Id, relative);
                _log.LogInformation("Thumbnail rendered for receipt {ReceiptId}", receipt.Id);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Thumbnail failed for receipt {ReceiptId}", receipt.Id);
            }
        }

        // 2. PDF - QuestPDF, was PdfSharp over GDI+
        string? pdfPath = null;
        try
        {
            pdfPath = ClaimPdfWriter.Write(_options.PdfRoot, claim);
            _claims.SetClaimPdfPath(claim.Id, pdfPath);
            _log.LogInformation("PDF written: {PdfPath}", pdfPath);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "PDF generation failed for claim {ClaimNumber}", claim.ClaimNumber);
        }

        // 3. Email the approver - SmtpClient, carried over unchanged
        if (!string.IsNullOrWhiteSpace(message.ApproverEmail))
        {
            var body = $"{message.EmployeeName} submitted expense claim {message.ClaimNumber} "
                     + $"for {message.TotalAmount:N2} USD.\r\n\r\nTitle: {message.Title}\r\n\r\n"
                     + $"Review it at {_options.WebBaseUrl}Claims/Details/{message.ClaimId}";

            _email.Send(message.ApproverEmail,
                        $"Expense claim {message.ClaimNumber} needs your decision", body, pdfPath);
        }
        else
        {
            _log.LogWarning("Claim {ClaimNumber} has no approver; nobody was emailed.", message.ClaimNumber);
        }

        // 4. Live toast to the claimant, still via the .NET Framework web app
        await _notifier.PushAsync(message.EmployeeEmail,
            $"Claim {message.ClaimNumber} submitted",
            "Your receipts were processed and the PDF is ready.", "success");
    }

    private async Task HandleDecidedAsync(ClaimDecidedMessage message)
    {
        var verdict = message.Approved ? "approved" : "rejected";

        var body = $"Your expense claim {message.ClaimNumber} ({message.TotalAmount:N2} USD) "
                 + $"was {verdict} by {message.DecidedByName}."
                 + (string.IsNullOrWhiteSpace(message.Reason) ? "" : $"\r\n\r\nComment: {message.Reason}");

        var claim = _claims.GetClaimWithDetails(message.ClaimId);
        var pdfPath = claim?.PdfPath is { Length: > 0 } p && File.Exists(p) ? p : null;

        _email.Send(message.EmployeeEmail, $"Expense claim {message.ClaimNumber} was {verdict}", body, pdfPath);

        await _notifier.PushAsync(message.EmployeeEmail,
            $"Claim {message.ClaimNumber} {verdict}",
            message.Approved
                ? "Finance will process your reimbursement."
                : message.Reason ?? "Open the claim to see why.",
            message.Approved ? "success" : "error");
    }
}
