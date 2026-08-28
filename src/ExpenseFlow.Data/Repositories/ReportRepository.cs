using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using ExpenseFlow.Domain.Reporting;

namespace ExpenseFlow.Data.Repositories
{
    /// <summary>
    /// Stored procedures via EF6 SqlQuery. The API changes in EF Core
    /// (SqlQuery on DbSet, or Database.SqlQueryRaw for keyless types),
    /// so these two methods are a small, contained migration item.
    /// </summary>
    public class ReportRepository : IReportRepository
    {
        private readonly ExpenseFlowContext _db;

        public ReportRepository(ExpenseFlowContext db)
        {
            if (db == null) throw new ArgumentNullException("db");
            _db = db;
        }

        public IList<DepartmentSpendRow> SpendByDepartment(DateTime fromUtc, DateTime toUtc)
        {
            return _db.Database.SqlQuery<DepartmentSpendRow>(
                "EXEC dbo.usp_SpendByDepartment @FromUtc, @ToUtc",
                new SqlParameter("@FromUtc", fromUtc),
                new SqlParameter("@ToUtc", toUtc)).ToList();
        }

        public IList<CategorySpendRow> SpendByCategory(DateTime fromUtc, DateTime toUtc)
        {
            return _db.Database.SqlQuery<CategorySpendRow>(
                "EXEC dbo.usp_SpendByCategory @FromUtc, @ToUtc",
                new SqlParameter("@FromUtc", fromUtc),
                new SqlParameter("@ToUtc", toUtc)).ToList();
        }
    }
}
