using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.SqlServer;
using System.Data.SqlClient;

namespace ExpenseFlow.Worker.Core.Data;

/// <summary>
/// EF6 on .NET Framework discovers its SQL Server provider through the
/// &lt;entityFramework&gt; section of app.config. There is no app.config here,
/// so the provider has to be registered in code or every query fails with
/// "No Entity Framework provider found".
///
/// Undocumented in most porting guides and a genuinely confusing failure the
/// first time you hit it. Recorded in the ledger as part of B5, and deleted
/// along with EF6 itself.
/// </summary>
public sealed class WorkerDbConfiguration : DbConfiguration
{
    public WorkerDbConfiguration()
    {
        SetProviderServices(SqlProviderServices.ProviderInvariantName, SqlProviderServices.Instance);
        SetProviderFactory(SqlProviderServices.ProviderInvariantName, SqlClientFactory.Instance);
        SetDefaultConnectionFactory(new SqlConnectionFactory());
    }
}
