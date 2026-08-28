using System;

namespace ExpenseFlow.Messaging.Contracts
{
    /// <summary>
    /// One queue carries several message types, so the body is an envelope
    /// with a discriminator. Hand-rolled because MSMQ's XmlMessageFormatter
    /// couples the queue to CLR types, which makes versioning miserable.
    /// </summary>
    public class MessageEnvelope
    {
        public string MessageType { get; set; }
        public string Payload { get; set; }
        public DateTime EnqueuedUtc { get; set; }
        public int DeliveryCount { get; set; }
        public string CorrelationId { get; set; }
    }
}
