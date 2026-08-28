using System;
using System.Collections.Generic;
using ExpenseFlow.Domain.Reporting;

namespace ExpenseFlow.Data.Repositories
{
    public interface IReportRepository
    {
        IList<DepartmentSpendRow> SpendByDepartment(DateTime fromUtc, DateTime toUtc);
        IList<CategorySpendRow> SpendByCategory(DateTime fromUtc, DateTime toUtc);
    }
}
