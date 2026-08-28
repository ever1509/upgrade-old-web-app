using System;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading;
using ExpenseFlow.Data;
using ExpenseFlow.Data.Repositories;
using ExpenseFlow.Messaging;
using ExpenseFlow.Messaging.Contracts;
using ExpenseFlow.Worker.Handlers;
using log4net;
using Newtonsoft.Json;

namespace ExpenseFlow.Worker
{
    /// <summary>
    /// The background processor. Blocking receive on MSMQ, one message at a
    /// time, on a dedicated thread.
    ///
    /// This class is the FIRST migration slice in the plan: it has no
    /// System.Web dependency at all, so once MSMQ, System.Drawing and
    /// PdfSharp are swapped it becomes a .NET 10 Worker Service that runs
    /// natively on macOS.
    /// </summary>
    public class WorkerLoop
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(WorkerLoop));

        private readonly EmailSender _email;
        private readonly NotificationPusher _notifier;
        private volatile bool _running;
        private Thread _thread;

        public WorkerLoop()
        {
            _email = new EmailSender(WorkerConfig.FromAddress);
            _notifier = new NotificationPusher(WorkerConfig.WebBaseUrl, WorkerConfig.InternalApiKey);
        }

        public void Start()
        {
            _running = true;
            _thread = new Thread(Run) { IsBackground = false, Name = "ExpenseFlow.WorkerLoop" };
            _thread.Start();
        }

        public void Stop()
        {
            _running = false;
            if (_thread != null) _thread.Join(TimeSpan.FromSeconds(15));
        }

        private void Run()
        {
            Log.InfoFormat("Listening on {0}", WorkerConfig.QueuePath);

            MsmqMessageReceiver receiver;
            try
            {
                receiver = new MsmqMessageReceiver(WorkerConfig.QueuePath, WorkerConfig.DeadLetterPath);
            }
            catch (Exception ex)
            {
                Log.Fatal("Could not open the MSMQ queues. Is the MSMQ Windows feature installed?", ex);
                return;
            }

            using (receiver)
            {
                while (_running)
                {
                    try
                    {
                        receiver.TryReceive(TimeSpan.FromSeconds(2), Handle);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Message handling failed; it will be retried or dead-lettered.", ex);
                        Thread.Sleep(1000);
                    }
                }
            }

            Log.Info("Worker loop stopped.");
        }

        private void Handle(MessageEnvelope envelope)
        {
            if (envelope == null) return;
            Log.InfoFormat("Received {0} ({1})", envelope.MessageType, envelope.CorrelationId);

            switch (envelope.MessageType)
            {
                case ClaimSubmittedMessage.Type:
                    HandleSubmitted(JsonConvert.DeserializeObject<ClaimSubmittedMessage>(envelope.Payload));
                    break;

                case ClaimDecidedMessage.Type:
                    HandleDecided(JsonConvert.DeserializeObject<ClaimDecidedMessage>(envelope.Payload));
                    break;

                default:
                    Log.WarnFormat("Unknown message type '{0}'; discarding.", envelope.MessageType);
                    break;
            }
        }

        /// <summary>
        /// Render every receipt thumbnail, build the claim PDF, email the
        /// approver, and toast the claimant. Four Windows-only dependencies
        /// in one method - by design.
        /// </summary>
        private void HandleSubmitted(ClaimSubmittedMessage message)
        {
            using (var db = new ExpenseFlowContext())
            {
                var claims = new ClaimRepository(db);
                var claim = claims.GetByIdWithDetails(message.ClaimId);
                if (claim == null)
                {
                    Log.WarnFormat("Claim {0} no longer exists.", message.ClaimId);
                    return;
                }

                // 1. Thumbnails (System.Drawing / GDI+)
                foreach (var receipt in claim.Lines.SelectMany(l => l.Receipts).ToList())
                {
                    if (receipt.HasThumbnail) continue;
                    if (!ThumbnailRenderer.IsRenderable(receipt.ContentType, receipt.FileName)) continue;

                    try
                    {
                        receipt.ThumbnailPath = ThumbnailRenderer.Render(WorkerConfig.UploadRoot, receipt.StoredPath);
                        Log.InfoFormat("Thumbnail rendered for receipt {0}", receipt.Id);
                    }
                    catch (Exception ex)
                    {
                        Log.Warn("Thumbnail failed for receipt " + receipt.Id, ex);
                    }
                }

                // 2. PDF (PdfSharp over GDI+)
                string pdfPath = null;
                try
                {
                    pdfPath = ClaimPdfWriter.Write(WorkerConfig.PdfRoot, claim);
                    claim.PdfPath = pdfPath;
                    Log.InfoFormat("PDF written: {0}", pdfPath);
                }
                catch (Exception ex)
                {
                    Log.Error("PDF generation failed for claim " + claim.ClaimNumber, ex);
                }

                db.SaveChanges();

                // 3. Email the approver (SmtpClient -> pickup directory)
                if (!string.IsNullOrWhiteSpace(message.ApproverEmail))
                {
                    var body = string.Format(
                        "{0} submitted expense claim {1} for {2:N2} USD.\r\n\r\nTitle: {3}\r\n\r\nReview it at {4}Claims/Details/{5}",
                        message.EmployeeName, message.ClaimNumber, message.TotalAmount,
                        message.Title, WorkerConfig.WebBaseUrl, message.ClaimId);

                    _email.Send(message.ApproverEmail,
                                "Expense claim " + message.ClaimNumber + " needs your decision",
                                body, pdfPath);
                }
                else
                {
                    Log.WarnFormat("Claim {0} has no approver; nobody was emailed.", message.ClaimNumber);
                }

                // 4. Live toast to the claimant (SignalR via the web app)
                _notifier.Push(message.EmployeeEmail,
                               "Claim " + message.ClaimNumber + " submitted",
                               "Your receipts were processed and the PDF is ready.",
                               "success");
            }
        }

        private void HandleDecided(ClaimDecidedMessage message)
        {
            var verdict = message.Approved ? "approved" : "rejected";

            var body = string.Format(
                "Your expense claim {0} ({1:N2} USD) was {2} by {3}.{4}",
                message.ClaimNumber, message.TotalAmount, verdict, message.DecidedByName,
                string.IsNullOrWhiteSpace(message.Reason) ? "" : "\r\n\r\nComment: " + message.Reason);

            string pdfPath = null;
            using (var db = new ExpenseFlowContext())
            {
                var claim = db.Claims.FirstOrDefault(c => c.Id == message.ClaimId);
                if (claim != null && !string.IsNullOrEmpty(claim.PdfPath) && File.Exists(claim.PdfPath))
                    pdfPath = claim.PdfPath;
            }

            _email.Send(message.EmployeeEmail,
                        "Expense claim " + message.ClaimNumber + " was " + verdict,
                        body, pdfPath);

            _notifier.Push(message.EmployeeEmail,
                           "Claim " + message.ClaimNumber + " " + verdict,
                           message.Approved
                               ? "Finance will process your reimbursement."
                               : (message.Reason ?? "Open the claim to see why."),
                           message.Approved ? "success" : "error");
        }
    }
}
