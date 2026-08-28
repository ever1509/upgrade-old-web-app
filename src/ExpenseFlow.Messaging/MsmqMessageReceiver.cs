using System;
using System.Messaging;
using ExpenseFlow.Messaging.Contracts;
using Newtonsoft.Json;

namespace ExpenseFlow.Messaging
{
    /// <summary>
    /// Blocking MSMQ receiver used by the Windows Service. Failed messages
    /// are retried up to MaxDeliveryCount, then moved to a dead-letter queue.
    /// </summary>
    public class MsmqMessageReceiver : IDisposable
    {
        public const int MaxDeliveryCount = 3;

        private readonly MessageQueue _queue;
        private readonly MessageQueue _deadLetter;

        public MsmqMessageReceiver(string queuePath, string deadLetterPath)
        {
            queuePath = string.IsNullOrWhiteSpace(queuePath) ? MsmqNames.DefaultQueuePath : queuePath;
            deadLetterPath = string.IsNullOrWhiteSpace(deadLetterPath) ? MsmqNames.DefaultDeadLetterPath : deadLetterPath;

            if (!MessageQueue.Exists(queuePath)) MessageQueue.Create(queuePath, true);
            if (!MessageQueue.Exists(deadLetterPath)) MessageQueue.Create(deadLetterPath, true);

            _queue = new MessageQueue(queuePath)
            {
                Formatter = new XmlMessageFormatter(new[] { typeof(string) })
            };
            _deadLetter = new MessageQueue(deadLetterPath)
            {
                Formatter = new XmlMessageFormatter(new[] { typeof(string) })
            };
        }

        /// <summary>
        /// Waits up to <paramref name="timeout"/> for one message and hands it
        /// to <paramref name="handler"/>. Returns false when nothing arrived.
        /// </summary>
        public bool TryReceive(TimeSpan timeout, Action<MessageEnvelope> handler)
        {
            MessageQueueTransaction tx = null;
            Message message = null;

            try
            {
                if (_queue.Transactional)
                {
                    tx = new MessageQueueTransaction();
                    tx.Begin();
                    message = _queue.Receive(timeout, tx);
                }
                else
                {
                    message = _queue.Receive(timeout);
                }
            }
            catch (MessageQueueException ex) when (ex.MessageQueueErrorCode == MessageQueueErrorCode.IOTimeout)
            {
                if (tx != null) tx.Abort();
                return false;
            }

            MessageEnvelope envelope = null;
            try
            {
                envelope = JsonConvert.DeserializeObject<MessageEnvelope>((string)message.Body);
                handler(envelope);
                if (tx != null) tx.Commit();
                return true;
            }
            catch (Exception)
            {
                // Roll the receive back, then decide whether to retry or bury it.
                if (tx != null) tx.Abort();

                if (envelope != null)
                {
                    envelope.DeliveryCount++;
                    if (envelope.DeliveryCount >= MaxDeliveryCount)
                        SendToDeadLetter(envelope);
                    else
                        Requeue(envelope);
                }
                throw;
            }
            finally
            {
                if (message != null) message.Dispose();
            }
        }

        private void Requeue(MessageEnvelope envelope)
        {
            SendTo(_queue, envelope);
        }

        private void SendToDeadLetter(MessageEnvelope envelope)
        {
            SendTo(_deadLetter, envelope);
        }

        private static void SendTo(MessageQueue queue, MessageEnvelope envelope)
        {
            var body = JsonConvert.SerializeObject(envelope);
            using (var msg = new Message(body, new XmlMessageFormatter(new[] { typeof(string) })))
            {
                msg.Label = envelope.MessageType;
                msg.Recoverable = true;
                if (queue.Transactional)
                    queue.Send(msg, MessageQueueTransactionType.Single);
                else
                    queue.Send(msg);
            }
        }

        public void Dispose()
        {
            if (_queue != null) _queue.Dispose();
            if (_deadLetter != null) _deadLetter.Dispose();
        }
    }
}
