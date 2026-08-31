using System;
using System.IO;
using System.Text;
using ExpenseFlow.Messaging.Contracts;
using Newtonsoft.Json;

namespace ExpenseFlow.Messaging
{
    /// <summary>
    /// A queue made of files in a folder.
    ///
    /// Unglamorous, but it is a real pattern from this era - plenty of
    /// line-of-business systems moved work between processes through a shared
    /// directory - and it keeps the important shape of the design: the web app
    /// hands work off and returns immediately, and a separate process picks it
    /// up later.
    ///
    /// Used here because MSMQ cannot be installed on current Windows. It is a
    /// bridge, not the destination: phase 4 replaces it with RabbitMQ once
    /// packages.config has become PackageReference.
    ///
    /// Writes are made to a temporary name and then renamed, so a reader can
    /// never observe a half-written message.
    /// </summary>
    public class FileSystemMessagePublisher : IMessagePublisher
    {
        private readonly string _directory;

        public FileSystemMessagePublisher(string directory)
        {
            _directory = string.IsNullOrWhiteSpace(directory)
                ? FileQueueNames.DefaultQueueDirectory
                : directory;
        }

        public void Publish(string messageType, object payload, string correlationId)
        {
            if (string.IsNullOrWhiteSpace(messageType))
                throw new ArgumentException("messageType is required", "messageType");

            var envelope = new MessageEnvelope
            {
                MessageType = messageType,
                Payload = JsonConvert.SerializeObject(payload),
                EnqueuedUtc = DateTime.UtcNow,
                DeliveryCount = 0,
                CorrelationId = correlationId ?? Guid.NewGuid().ToString("N")
            };

            FileQueueNames.EnsureDirectories(_directory);

            // Sortable name: the receiver processes strictly oldest-first.
            var name = envelope.EnqueuedUtc.ToString("yyyyMMdd'T'HHmmss'.'fffffff")
                       + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);

            var temp = Path.Combine(_directory, name + ".tmp");
            var final = Path.Combine(_directory, name + ".json");

            File.WriteAllText(temp, JsonConvert.SerializeObject(envelope), Encoding.UTF8);
            File.Move(temp, final);
        }
    }
}
