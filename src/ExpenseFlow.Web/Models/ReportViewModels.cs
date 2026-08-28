using System;
using System.Collections.Generic;
using ExpenseFlow.Domain.Reporting;

namespace ExpenseFlow.Web.Models
{
    public class ReportsViewModel
    {
        public DateTime FromUtc { get; set; }
        public DateTime ToUtc { get; set; }
        public IList<DepartmentSpendRow> ByDepartment { get; set; }
        public IList<CategorySpendRow> ByCategory { get; set; }

        public ReportsViewModel()
        {
            ToUtc = DateTime.UtcNow.Date.AddDays(1);
            FromUtc = ToUtc.AddMonths(-12);
            ByDepartment = new List<DepartmentSpendRow>();
            ByCategory = new List<CategorySpendRow>();
        }
    }
}
