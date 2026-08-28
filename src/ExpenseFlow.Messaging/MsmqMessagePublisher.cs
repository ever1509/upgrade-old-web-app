using System;
using System.Messaging;
using ExpenseFlow.Messaging.Contracts;
using Newtonsoft.Json;

namespace ExpenseFlow.Messaging
{
    /// <summary>
    /// MSMQ publisher. Creates the private queue on first use if it is
    /// missing, which requires the MSMQ Windows feature to be installed.
    /// </summary>
    public class MsmqMessagePublisher : IMessagePublisher, IDisposable
    {
        private readonly string _queuePath;
        private MessageQueue _queue;
        private readonly object _sync = new object();

        public MsmqMessagePublisher(string queuePath)
        {
            _queuePath = string.IsNullOrWhiteSpace(queuePath) ? MsmqNames.DefaultQueuePath : queuePath;
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

            var body = JsonConvert.SerializeObject(envelope);
            var queue = EnsureQueue();

            using (var message = new Message(body, new XmlMessageFormatter(new[] { typeof(string) })))
            {
                message.Label = messageType;
                message.Recoverable = true;

                // Transactional send so the message survives a queue-service
                // restart. Note this is an MSMQ transaction, NOT the same
                // transaction as the database write - the classic dual-write
                // problem you solve with an outbox after the migration.
                if (queue.Transactional)
                    queue.Send(message, MessageQueueTransactionType.Single);
                else
                    queue.Send(message);
            }
        }

        private MessageQueue EnsureQueue()
        {
            if (_queue != null) return _queue;

            lock (_sync)
            {
                if (_queue != null) return _queue;

                if (!MessageQueue.Exists(_queuePath))
                    MessageQueue.Create(_queuePath, true);

                _queue = new MessageQueue(_queuePath)
                {
                    Formatter = new XmlMessageFormatter(new[] { typeof(string) })
                };
                return _queue;
            }
        }

        public void Dispose()
        {
            if (_queue == null) return;
            _queue.Dispose();
            _queue = null;
        }
    }
}
