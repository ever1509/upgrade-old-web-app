using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace ExpenseFlow.Worker.Core.Handlers;

/// <summary>
/// Ledger item C1: System.Net.Mail.SmtpClient, carried over rather than
/// replaced.
///
/// It is obsolete (SYSLIB0014) but fully functional on .NET 10, so it crosses
/// the migration untouched. Recording that is the point: knowing which
/// dependencies do NOT block you is half of an assessment, and swapping in
/// MailKit here would have been effort spent for no migration benefit. It is
/// phase 7 work, not phase 5.
///
/// One thing genuinely did change. On .NET Framework the pickup directory
/// came from &lt;system.net&gt;&lt;mailSettings&gt; in app.config. There is no
/// app.config here, so it is configured in code from appsettings.json.
/// </summary>
public sealed class EmailSender
{
    private readonly ILogger<EmailSender> _log;
    private readonly WorkerOptions _options;

    public EmailSender(ILogger<EmailSender> log, IOptions<WorkerOptions> options)
    {
        _log = log;
        _options = options.Value;
    }

    public void Send(string? to, string subject, string body, string? attachmentPath)
    {
        if (string.IsNullOrWhiteSpace(to))
        {
            _log.LogWarning("No recipient address; skipping email: {Subject}", subject);
            return;
        }

        using var message = new MailMessage(_options.FromAddress, to)
        {
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };

        Attachment? attachment = null;
        try
        {
            if (!string.IsNullOrEmpty(attachmentPath) && File.Exists(attachmentPath))
            {
                attachment = new Attachment(attachmentPath);
                message.Attachments.Add(attachment);
            }

            Directory.CreateDirectory(_options.MailPickupDirectory);

#pragma warning disable SYSLIB0014 // SmtpClient is obsolete; see the class comment.
            using var client = new SmtpClient
            {
                DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
                PickupDirectoryLocation = _options.MailPickupDirectory
            };
            client.Send(message);
#pragma warning restore SYSLIB0014

            _log.LogInformation("Email queued for {Recipient}: {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Could not send email to {Recipient}", to);
            throw;
        }
        finally
        {
            attachment?.Dispose();
        }
    }
}
