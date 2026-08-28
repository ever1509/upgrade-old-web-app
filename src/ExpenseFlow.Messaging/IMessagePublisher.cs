namespace ExpenseFlow.Messaging
{
    /// <summary>
    /// The seam that saves you later. The web app depends on this interface,
    /// not on System.Messaging, so swapping MSMQ for RabbitMQ in phase 4 is
    /// a one-class change while everything is still on .NET Framework.
    /// </summary>
    public interface IMessagePublisher
    {
        void Publish(string messageType, object payload, string correlationId);
    }
}
