using System;
using System.Collections.Generic;
using System.Linq;
using ExpenseFlow.Domain.Enums;

namespace ExpenseFlow.Domain.Entities
{
    public class ExpenseClaim
    {
        public ExpenseClaim()
        {
            Lines = new List<ExpenseLine>();
            History = new List<ApprovalHistory>();
            Status = ClaimStatus.Draft.ToString();
            CreatedUtc = DateTime.UtcNow;
        }

        public int Id { get; set; }
        public string ClaimNumber { get; set; }
        public int EmployeeId { get; set; }
        public int? ProjectId { get; set; }
        public string Title { get; set; }
        public string Status { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime? SubmittedUtc { get; set; }
        public DateTime? DecidedUtc { get; set; }
        public int? DecidedByEmployeeId { get; set; }
        public string RejectionReason { get; set; }
        public string PdfPath { get; set; }
        public DateTime CreatedUtc { get; set; }
        public byte[] RowVersion { get; set; }

        public virtual Employee Employee { get; set; }
        public virtual Project Project { get; set; }
        public virtual Employee DecidedBy { get; set; }
        public virtual ICollection<ExpenseLine> Lines { get; set; }
        public virtual ICollection<ApprovalHistory> History { get; set; }

        public ClaimStatus StatusValue
        {
            get
            {
                ClaimStatus parsed;
                return Enum.TryParse(Status, true, out parsed) ? parsed : ClaimStatus.Draft;
            }
            set { Status = value.ToString(); }
        }

        public bool IsEditable
        {
            get { return StatusValue == ClaimStatus.Draft || StatusValue == ClaimStatus.Rejected; }
        }

        public decimal CalculateTotal()
        {
            return Lines == null ? 0m : Lines.Sum(l => l.Amount);
        }
    }
}
