using System.IO;

namespace ExpenseFlow.Messaging
{
    public static class FileQueueNames
    {
        public const string DefaultQueueDirectory = @"C:\ExpenseFlow\queue";

        public const string ProcessingFolder = "processing";
        public const string DeadLetterFolder = "dead";

        public static string ProcessingPath(string queueDirectory)
        {
            return Path.Combine(queueDirectory, ProcessingFolder);
        }

        public static string DeadLetterPath(string queueDirectory)
        {
            return Path.Combine(queueDirectory, DeadLetterFolder);
        }

        public static void EnsureDirectories(string queueDirectory)
        {
            if (!Directory.Exists(queueDirectory)) Directory.CreateDirectory(queueDirectory);
            var processing = ProcessingPath(queueDirectory);
            var dead = DeadLetterPath(queueDirectory);
            if (!Directory.Exists(processing)) Directory.CreateDirectory(processing);
            if (!Directory.Exists(dead)) Directory.CreateDirectory(dead);
        }
    }
}
