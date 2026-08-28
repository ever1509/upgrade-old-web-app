using System.ServiceProcess;

namespace ExpenseFlow.Worker
{
    /// <summary>
    /// *** WINDOWS-ONLY HOST ***
    ///
    /// System.ServiceProcess.ServiceBase is Windows-only. On .NET 10 the
    /// equivalent is a generic-host BackgroundService, optionally wrapped by
    /// Microsoft.Extensions.Hosting.WindowsServices when you still want it
    /// registered as a Windows Service - and that same host runs unchanged
    /// as a plain process on macOS and Linux.
    /// </summary>
    public class WorkerService : ServiceBase
    {
        public const string ServiceNameConstant = "ExpenseFlowWorker";

        private readonly WorkerLoop _loop = new WorkerLoop();

        public WorkerService()
        {
            ServiceName = ServiceNameConstant;
            CanStop = true;
            CanShutdown = true;
        }

        protected override void OnStart(string[] args)
        {
            _loop.Start();
        }

        protected override void OnStop()
        {
            _loop.Stop();
        }

        protected override void OnShutdown()
        {
            _loop.Stop();
        }
    }
}
