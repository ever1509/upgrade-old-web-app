using System;

namespace ExpenseFlow.Messaging
{
    /// <summary>
    /// Chooses a transport from configuration.
    ///
    /// Both implementations are kept deliberately. MSMQ is the original and
    /// stays in the repository as the "before" half of the migration story,
    /// even though current Windows will not run it. The file queue is the
    /// bridge that keeps the pipeline working today.
    ///
    /// At phase 4 a third arm appears here for RabbitMQ, and this class is the
    /// only place that has to know.
    /// </summary>
    public static class MessagingFactory
    {
        public const string Msmq = "msmq";
        public const string File = "file";

        public static IMessagePublisher CreatePublisher(string transport, string msmqPath, string queueDirectory)
        {
            return IsMsmq(transport)
                ? (IMessagePublisher)new MsmqMessagePublisher(msmqPath)
                : new FileSystemMessagePublisher(queueDirectory);
        }

        public static IMessageReceiver CreateReceiver(string transport, string msmqPath,
                                                      string msmqDeadLetterPath, string queueDirectory)
        {
            return IsMsmq(transport)
                ? (IMessageReceiver)new MsmqMessageReceiver(msmqPath, msmqDeadLetterPath)
                : new FileSystemMessageReceiver(queueDirectory);
        }

        public static string Describe(string transport, string msmqPath, string queueDirectory)
        {
            return IsMsmq(transport) ? "MSMQ " + msmqPath : "file queue " + queueDirectory;
        }

        private static bool IsMsmq(string transport)
        {
            return string.Equals(transport, Msmq, StringComparison.OrdinalIgnoreCase);
        }
    }
}
