namespace ExpenseFlow.Messaging.Contracts
{
    /// <summary>
    /// Published when an employee submits a claim. The worker renders
    /// receipt thumbnails, builds the PDF, and emails the approver.
    /// </summary>
    public class ClaimSubmittedMessage
    {
        public const string Type = "claim.submitted";

        public int ClaimId { get; set; }
        public string ClaimNumber { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public string EmployeeEmail { get; set; }
        public int? ApproverEmployeeId { get; set; }
        public string ApproverEmail { get; set; }
        public decimal TotalAmount { get; set; }
        public string Title { get; set; }
    }
}
