using System.Configuration;

namespace ExpenseFlow.Worker
{
    public static class WorkerConfig
    {
        public static string Transport { get { return Get("ExpenseFlow:Transport", "file"); } }
        public static string QueueDirectory { get { return Get("ExpenseFlow:QueueDirectory", @"C:\ExpenseFlow\queue"); } }
        public static string QueuePath { get { return Get("ExpenseFlow:QueuePath", @".\private$\expenseflow"); } }
        public static string DeadLetterPath { get { return Get("ExpenseFlow:DeadLetterPath", @".\private$\expenseflow_dead"); } }
        public static string UploadRoot { get { return Get("ExpenseFlow:UploadRoot", @"C:\ExpenseFlow\uploads"); } }
        public static string PdfRoot { get { return Get("ExpenseFlow:PdfRoot", @"C:\ExpenseFlow\pdf"); } }
        public static string WebBaseUrl { get { return Get("ExpenseFlow:WebBaseUrl", "http://localhost:52080/"); } }
        public static string InternalApiKey { get { return Get("ExpenseFlow:InternalApiKey", "local-dev-worker-key"); } }
        public static string FromAddress { get { return Get("ExpenseFlow:FromAddress", "expenseflow@localhost"); } }

        private static string Get(string key, string fallback)
        {
            var value = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
    }
}
