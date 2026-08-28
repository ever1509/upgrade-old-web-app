using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace ExpenseFlow.Worker
{
    /// <summary>
    /// Lets the exe register itself with "installutil". Legacy deployment
    /// mechanics; on .NET 10 you would use "sc create" or the Windows
    /// Service hosting package instead.
    /// </summary>
    [RunInstaller(true)]
    public class WorkerServiceInstaller : Installer
    {
        public WorkerServiceInstaller()
        {
            var process = new ServiceProcessInstaller { Account = ServiceAccount.LocalSystem };

            var service = new ServiceInstaller
            {
                ServiceName = WorkerService.ServiceNameConstant,
                DisplayName = "ExpenseFlow Background Worker",
                Description = "Processes submitted expense claims: thumbnails, PDFs and notifications.",
                StartType = ServiceStartMode.Automatic
            };

            Installers.Add(process);
            Installers.Add(service);
        }
    }
}
