namespace ExpenseFlow.Domain.Reporting
{
    /// <summary>Shape returned by dbo.usp_SpendByDepartment.</summary>
    public class DepartmentSpendRow
    {
        public string Department { get; set; }
        public int ClaimCount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal AverageAmount { get; set; }
    }
}
