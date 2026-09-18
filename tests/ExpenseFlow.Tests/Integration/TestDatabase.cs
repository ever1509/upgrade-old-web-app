using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.SqlServer;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ExpenseFlow.Data;
using ExpenseFlow.Domain.Entities;
using Xunit;

namespace ExpenseFlow.Tests.Integration
{
    /// <summary>
    /// Builds a throwaway database from the real db/*.sql scripts, once per
    /// test run, so the integration tests exercise the same schema, seed data
    /// and stored procedures as the application - never a hand-maintained copy.
    ///
    /// Where it runs:
    ///   * EXPENSEFLOW_TEST_CONNECTION set  -> that server
    ///   * otherwise, on Windows            -> .\SQLEXPRESS, Windows auth
    ///   * otherwise                        -> every integration test is skipped
    ///
    /// Each target framework gets its own database, because `dotnet test`
    /// runs the net48 and net10.0 legs in parallel and they would otherwise
    /// drop each other's database mid-run.
    /// </summary>
    public sealed class TestDatabase : IDisposable
    {
        public const string SkipReason =
            "No test database. Set EXPENSEFLOW_TEST_CONNECTION, or run on Windows with SQL Server Express at .\\SQLEXPRESS.";

#if NET48
        public const string DatabaseName = "ExpenseFlow_Tests_net48";
#else
        public const string DatabaseName = "ExpenseFlow_Tests_net10";
#endif

        private static readonly object ConfigLock = new object();
        private static bool _efConfigured;

        public TestDatabase()
        {
            if (ServerConnectionString == null) return;

            ConfigureEntityFramework();
            Recreate();
            ConnectionString = WithDatabase(ServerConnectionString, DatabaseName);
        }

        /// <summary>Connection to the throwaway database. Null when tests are skipped.</summary>
        public string ConnectionString { get; private set; }

        /// <summary>Server-level connection string, or null if no database is available here.</summary>
        public static string ServerConnectionString
        {
            get
            {
                var fromEnv = Environment.GetEnvironmentVariable("EXPENSEFLOW_TEST_CONNECTION");
                if (!string.IsNullOrWhiteSpace(fromEnv)) return fromEnv;

                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                    return @"Data Source=.\SQLEXPRESS;Integrated Security=True;MultipleActiveResultSets=True";

                return null;
            }
        }

        public ExpenseFlowContext NewContext()
        {
            return new ExpenseFlowContext(ConnectionString);
        }

        // ---------------- test data helpers ----------------

        public Employee Employee(string email)
        {
            using (var db = NewContext())
                return db.Employees.AsNoTracking().Single(e => e.Email == email);
        }

        /// <summary>
        /// Inserts a claim for the given employee with one Meals line, optionally
        /// with a receipt. Unless a claim number is supplied it gets a unique
        /// non-CLM one, so tests never collide with each other or the seed data.
        /// </summary>
        public ExpenseClaim InsertClaim(string employeeEmail, string status, decimal amount,
                                        bool withReceipt = false, DateTime? decidedUtc = null,
                                        string claimNumber = null)
        {
            using (var db = NewContext())
            {
                var employee = db.Employees.Single(e => e.Email == employeeEmail);
                var meals = db.Categories.Single(c => c.Code == "MEALS");
                var project = db.Projects.Single(p => p.Code == "PRJ-APOLLO");

                var claim = new ExpenseClaim
                {
                    ClaimNumber = claimNumber ?? "T-" + Guid.NewGuid().ToString("N").Substring(0, 10).ToUpperInvariant(),
                    EmployeeId = employee.Id,
                    ProjectId = project.Id,
                    Title = "Integration test " + Guid.NewGuid().ToString("N").Substring(0, 6),
                    Status = status,
                    TotalAmount = amount,
                    SubmittedUtc = status == "Draft" ? (DateTime?)null : DateTime.UtcNow,
                    DecidedUtc = decidedUtc,
                    CreatedUtc = DateTime.UtcNow
                };

                var line = new ExpenseLine
                {
                    CategoryId = meals.Id,
                    ExpenseDate = DateTime.UtcNow.Date.AddDays(-1),
                    Description = "Integration test line",
                    Amount = amount,
                    Currency = "USD"
                };

                if (withReceipt)
                {
                    line.Receipts.Add(new Receipt
                    {
                        FileName = "receipt.jpg",
                        StoredPath = "it/receipt.jpg",
                        ContentType = "image/jpeg",
                        SizeBytes = 1024,
                        UploadedUtc = DateTime.UtcNow
                    });
                }

                claim.Lines.Add(line);
                db.Claims.Add(claim);
                db.SaveChanges();
                return claim;
            }
        }

        // ---------------- setup ----------------

        /// <summary>
        /// EF6 normally reads its provider from the &lt;entityFramework&gt; section of
        /// a config file. Registering it in code instead works the same on both
        /// frameworks, so neither leg depends on an app.config.
        /// </summary>
        private static void ConfigureEntityFramework()
        {
            lock (ConfigLock)
            {
                if (_efConfigured) return;
                DbConfiguration.SetConfiguration(new TestDbConfiguration());
                _efConfigured = true;
            }
        }

        private static void Recreate()
        {
            SqlConnection.ClearAllPools();

            var master = WithDatabase(ServerConnectionString, "master");
            Execute(master,
                "IF DB_ID('" + DatabaseName + "') IS NOT NULL BEGIN " +
                "ALTER DATABASE [" + DatabaseName + "] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                "DROP DATABASE [" + DatabaseName + "]; END");
            Execute(master, "CREATE DATABASE [" + DatabaseName + "]");

            var target = WithDatabase(ServerConnectionString, DatabaseName);
            foreach (var script in new[] { "01_schema.sql", "02_seed.sql", "03_reporting_procs.sql" })
            {
                var sql = File.ReadAllText(Path.Combine(FindDbFolder(), script));

                // The scripts target the application database by name. Point
                // them at the throwaway one instead. "ExpenseFlow" appears
                // bracketed or quoted only in CREATE/USE/DB_ID, never in data.
                sql = sql.Replace("[ExpenseFlow]", "[" + DatabaseName + "]")
                         .Replace("'ExpenseFlow'", "'" + DatabaseName + "'");

                foreach (var batch in SplitOnGo(sql))
                    Execute(target, batch);
            }
        }

        /// <summary>GO is a sqlcmd/SSMS separator, not T-SQL, so ADO.NET cannot run it.</summary>
        private static string[] SplitOnGo(string sql)
        {
            return Regex.Split(sql, @"^\s*GO\s*;?\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
                        .Select(b => b.Trim())
                        .Where(b => b.Length > 0)
                        .ToArray();
        }

        private static void Execute(string connectionString, string sql)
        {
            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(sql, connection) { CommandTimeout = 120 })
            {
                connection.Open();
                command.ExecuteNonQuery();
            }
        }

        private static string WithDatabase(string connectionString, string database)
        {
            var builder = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = database };
            return builder.ConnectionString;
        }

        private static string FindDbFolder()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "db", "01_schema.sql");
                if (File.Exists(candidate)) return Path.Combine(dir.FullName, "db");
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException("Could not find the repository's db/ folder above " + AppContext.BaseDirectory);
        }

        public void Dispose()
        {
            // The database is left in place after the run so it can be
            // inspected when a test fails. It is dropped and rebuilt next run.
            SqlConnection.ClearAllPools();
        }

        private sealed class TestDbConfiguration : DbConfiguration
        {
            public TestDbConfiguration()
            {
                SetProviderServices(SqlProviderServices.ProviderInvariantName, SqlProviderServices.Instance);
                SetProviderFactory(SqlProviderServices.ProviderInvariantName, SqlClientFactory.Instance);
                SetDefaultConnectionFactory(new SqlConnectionFactory());
            }
        }
    }

    [CollectionDefinition(Name)]
    public sealed class DatabaseCollection : ICollectionFixture<TestDatabase>
    {
        public const string Name = "Database";
    }

    /// <summary>A [Fact] that skips itself when no test database is reachable.</summary>
    public sealed class DatabaseFactAttribute : FactAttribute
    {
        public DatabaseFactAttribute()
        {
            if (TestDatabase.ServerConnectionString == null) Skip = TestDatabase.SkipReason;
        }
    }
}
