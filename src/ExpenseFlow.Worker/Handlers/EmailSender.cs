using System;
using System.IO;
using System.Net.Mail;
using log4net;

namespace ExpenseFlow.Worker.Handlers
{
    /// <summary>
    /// System.Net.Mail.SmtpClient. Marked obsolete by Microsoft since .NET
    /// Core 2.0 ("not recommended for new development") but still present,
    /// so this one compiles on .NET 10 and merely warns.
    ///
    /// A good example of a NON-blocker for the assessment ledger: it will
    /// cross over, and the MailKit rewrite can wait until after the
    /// migration. Knowing the difference between "blocks the port" and
    /// "should be modernised eventually" is most of what phase 3 is for.
    ///
    /// Locally this writes .eml files to the pickup directory configured in
    /// App.config, so no SMTP server is required.
    /// </summary>
    public class EmailSender
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(EmailSender));
        private readonly string _from;

        public EmailSender(string fromAddress)
        {
            _from = string.IsNullOrWhiteSpace(fromAddress) ? "expenseflow@localhost" : fromAddress;
        }

        public void Send(string to, string subject, string body, string attachmentPath)
        {
            if (string.IsNullOrWhiteSpace(to))
            {
                Log.Warn("No recipient address; skipping email: " + subject);
                return;
            }

            using (var message = new MailMessage(_from, to))
            {
                message.Subject = subject;
                message.Body = body;
                message.IsBodyHtml = false;

                Attachment attachment = null;
                try
                {
                    if (!string.IsNullOrEmpty(attachmentPath) && File.Exists(attachmentPath))
                    {
                        attachment = new Attachment(attachmentPath);
                        message.Attachments.Add(attachment);
                    }

                    using (var client = new SmtpClient())
                    {
                        client.Send(message);
                    }

                    Log.InfoFormat("Email queued for {0}: {1}", to, subject);
                }
                catch (Exception ex)
                {
                    Log.Error("Could not send email to " + to, ex);
                    throw;
                }
                finally
                {
                    if (attachment != null) attachment.Dispose();
                }
            }
        }
    }
}
