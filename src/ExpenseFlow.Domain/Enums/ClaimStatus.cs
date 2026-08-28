namespace ExpenseFlow.Domain.Enums
{
    /// <summary>
    /// Persisted as a string column. EF6 has no value converters, so the
    /// entity carries a string and exposes this enum through a helper.
    /// EF Core fixes this with HasConversion&lt;string&gt;() - note it during
    /// the assessment phase.
    /// </summary>
    public enum ClaimStatus
    {
        Draft = 0,
        Submitted = 1,
        Approved = 2,
        Rejected = 3,
        Reimbursed = 4
    }
}
