using System;
using ExpenseFlow.Messaging.Contracts;

namespace ExpenseFlow.Messaging
{
    /// <summary>
    /// The consuming half of the seam. The worker depends on this, never on a
    /// concrete transport, which is what lets MSMQ be swapped for a folder
    /// today and for RabbitMQ at phase 4 without the worker changing at all.
    /// </summary>
    public interface IMessageReceiver : IDisposable
    {
        /// <summary>
        /// Waits up to <paramref name="timeout"/> for one message and hands it
        /// to <paramref name="handler"/>. Returns false when nothing arrived.
        /// A handler that throws must leave the message retryable.
        /// </summary>
        bool TryReceive(TimeSpan timeout, Action<MessageEnvelope> handler);
    }
}
