using System;
using System.IO;
using System.ServiceProcess;
using System.Threading;
using ExpenseFlow.Messaging;
using log4net;
using log4net.Config;

namespace ExpenseFlow.Worker
{
    public static class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Program));

        public static int Main(string[] args)
        {
            XmlConfigurator.Configure();
            EnsureDirectories();

            var console = args != null && Array.Exists(args,
                a => a.Equals("--console", StringComparison.OrdinalIgnoreCase) ||
                     a.Equals("-c", StringComparison.OrdinalIgnoreCase));

            if (console || Environment.UserInteractive)
            {
                return RunInteractive();
            }

            ServiceBase.Run(new ServiceBase[] { new WorkerService() });
            return 0;
        }

        private static int RunInteractive()
        {
            Console.WriteLine("ExpenseFlow worker - console mode. Press Ctrl+C to stop.");
            Console.WriteLine("  queue   : " + MessagingFactory.Describe(
                WorkerConfig.Transport, WorkerConfig.QueuePath, WorkerConfig.QueueDirectory));
            Console.WriteLine("  uploads : " + WorkerConfig.UploadRoot);
            Console.WriteLine("  pdf     : " + WorkerConfig.PdfRoot);
            Console.WriteLine("  web     : " + WorkerConfig.WebBaseUrl);
            Console.WriteLine();

            var loop = new WorkerLoop();
            var stopped = new ManualResetEventSlim(false);

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                Console.WriteLine("Stopping...");
                stopped.Set();
            };

            try
            {
                loop.Start();
                stopped.Wait();
                loop.Stop();
                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal("Worker terminated unexpectedly.", ex);
                return 1;
            }
        }

        private static void EnsureDirectories()
        {
            foreach (var path in new[] { WorkerConfig.UploadRoot, WorkerConfig.PdfRoot, WorkerConfig.QueueDirectory })
            {
                try
                {
                    if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                }
                catch (Exception ex)
                {
                    Log.Warn("Could not create " + path, ex);
                }
            }
        }
    }
}
