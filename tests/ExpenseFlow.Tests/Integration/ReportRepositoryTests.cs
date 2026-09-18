using System;
using System.Linq;
using ExpenseFlow.Data.Repositories;
using Xunit;

namespace ExpenseFlow.Tests.Integration
{
    /// <summary>
    /// The two stored procedures, called through EF6 SqlQuery&lt;T&gt;. These pin
    /// the column-to-property mapping, which is exactly what changes when the
    /// call moves to EF Core's Database.SqlQueryRaw (ledger C2).
    /// </summary>
    [Collection(DatabaseCollection.Name)]
    public class ReportRepositoryTests
    {
        private readonly TestDatabase _db;

        public ReportRepositoryTests(TestDatabase db)
        {
            _db = db;
        }

        [DatabaseFact]
        public void Spend_by_department_counts_approved_claims_and_ignores_undecided_ones()
        {
            _db.InsertClaim("alice@expenseflow.local", "Approved", 123.45m, decidedUtc: DateTime.UtcNow);
            _db.InsertClaim("carla@expenseflow.local", "Submitted", 999m);    // Sales, undecided

            using (var ctx = _db.NewContext())
            {
                var rows = new ReportRepository(ctx)
                    .SpendByDepartment(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));

                var engineering = Assert.Single(rows, r => r.Department == "Engineering");
                Assert.True(engineering.ClaimCount >= 1);
                Assert.True(engineering.TotalAmount >= 123.45m);
                Assert.True(engineering.AverageAmount > 0m);

                Assert.DoesNotContain(rows, r => r.Department == "Sales");
            }
        }

        [DatabaseFact]
        public void Spend_by_category_reports_approved_lines()
        {
            _db.InsertClaim("alice@expenseflow.local", "Approved", 77.70m, decidedUtc: DateTime.UtcNow);

            using (var ctx = _db.NewContext())
            {
                var rows = new ReportRepository(ctx)
                    .SpendByCategory(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));

                var meals = Assert.Single(rows, r => r.Category == "Meals");
                Assert.True(meals.LineCount >= 1);
                Assert.True(meals.TotalAmount >= 77.70m);
            }
        }

        [DatabaseFact]
        public void Claims_decided_outside_the_range_are_excluded()
        {
            using (var ctx = _db.NewContext())
            {
                var rows = new ReportRepository(ctx)
                    .SpendByDepartment(new DateTime(2000, 1, 1), new DateTime(2000, 1, 2));

                Assert.Empty(rows);
            }
        }
    }
}
