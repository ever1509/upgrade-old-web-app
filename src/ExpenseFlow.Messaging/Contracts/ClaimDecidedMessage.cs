namespace ExpenseFlow.Messaging.Contracts
{
    /// <summary>
    /// Published when a claim is approved or rejected. The worker emails
    /// the claimant and pushes a SignalR notification to their browser.
    /// </summary>
    public class ClaimDecidedMessage
    {
        public const string Type = "claim.decided";

        public int ClaimId { get; set; }
        public string ClaimNumber { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeEmail { get; set; }
        public string EmployeeName { get; set; }
        public bool Approved { get; set; }
        public string DecidedByName { get; set; }
        public string Reason { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
