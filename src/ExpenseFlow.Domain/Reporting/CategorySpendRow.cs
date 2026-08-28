namespace ExpenseFlow.Domain.Reporting
{
    /// <summary>Shape returned by dbo.usp_SpendByCategory.</summary>
    public class CategorySpendRow
    {
        public string Category { get; set; }
        public int LineCount { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
