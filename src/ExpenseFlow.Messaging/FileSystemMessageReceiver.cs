using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using ExpenseFlow.Messaging.Contracts;
using Newtonsoft.Json;

namespace ExpenseFlow.Messaging
{
    /// <summary>
    /// Polls a folder for message files, oldest first.
    ///
    /// Claiming is done by moving the file into a "processing" folder: the move
    /// either succeeds or throws, so two consumers can never take the same
    /// message. On success the file is deleted; on failure the delivery count is
    /// incremented and the message is either returned to the queue or moved to
    /// "dead" once it has failed MaxDeliveryCount times.
    ///
    /// Same at-least-once semantics as the MSMQ receiver, so the worker's
    /// handling code is unchanged.
    /// </summary>
    public class FileSystemMessageReceiver : IMessageReceiver
    {
        public const int MaxDeliveryCount = 3;

        private readonly string _directory;
        private readonly string _processing;
        private readonly string _dead;

        public FileSystemMessageReceiver(string directory)
        {
            _directory = string.IsNullOrWhiteSpace(directory)
                ? FileQueueNames.DefaultQueueDirectory
                : directory;

            FileQueueNames.EnsureDirectories(_directory);
            _processing = FileQueueNames.ProcessingPath(_directory);
            _dead = FileQueueNames.DeadLetterPath(_directory);

            RecoverAbandonedMessages();
        }

        public bool TryReceive(TimeSpan timeout, Action<MessageEnvelope> handler)
        {
            var claimed = WaitForMessage(timeout);
            if (claimed == null) return false;

            MessageEnvelope envelope;
            try
            {
                envelope = JsonConvert.DeserializeObject<MessageEnvelope>(File.ReadAllText(claimed));
            }
            catch (Exception)
            {
                // Unreadable: never retryable, so bury it immediately.
                MoveTo(claimed, _dead);
                throw;
            }

            try
            {
                handler(envelope);
                File.Delete(claimed);
                return true;
            }
            catch (Exception)
            {
                envelope.DeliveryCount++;

                if (envelope.DeliveryCount >= MaxDeliveryCount)
                {
                    File.WriteAllText(claimed, JsonConvert.SerializeObject(envelope), Encoding.UTF8);
                    MoveTo(claimed, _dead);
                }
                else
                {
                    File.WriteAllText(claimed, JsonConvert.SerializeObject(envelope), Encoding.UTF8);
                    MoveTo(claimed, _directory);
                }
                throw;
            }
        }

        /// <summary>Polls until a message is claimed or the timeout expires.</summary>
        private string WaitForMessage(TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow.Add(timeout);

            do
            {
                var claimed = TryClaimOldest();
                if (claimed != null) return claimed;

                if (DateTime.UtcNow >= deadline) return null;
                Thread.Sleep(250);
            }
            while (true);
        }

        private string TryClaimOldest()
        {
            string[] files;
            try
            {
                files = Directory.GetFiles(_directory, "*.json");
            }
            catch (DirectoryNotFoundException)
            {
                FileQueueNames.EnsureDirectories(_directory);
                return null;
            }

            foreach (var path in files.OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal))
            {
                var target = Path.Combine(_processing, Path.GetFileName(path));
                try
                {
                    if (File.Exists(target)) File.Delete(target);
                    File.Move(path, target);
                    return target;
                }
                catch (IOException)
                {
                    // Someone else claimed it, or it is still being written. Skip.
                }
            }

            return null;
        }

        /// <summary>
        /// Anything left in "processing" is from a worker that died mid-message.
        /// Put it back so it is retried rather than lost.
        /// </summary>
        private void RecoverAbandonedMessages()
        {
            foreach (var path in Directory.GetFiles(_processing, "*.json"))
            {
                try { MoveTo(path, _directory); }
                catch (IOException) { }
            }
        }

        private static void MoveTo(string path, string targetDirectory)
        {
            var target = Path.Combine(targetDirectory, Path.GetFileName(path));
            if (File.Exists(target)) File.Delete(target);
            File.Move(path, target);
        }

        public void Dispose() { }
    }
}
