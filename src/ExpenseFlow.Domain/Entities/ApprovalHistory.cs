using System;

namespace ExpenseFlow.Domain.Entities
{
    public class ApprovalHistory
    {
        public ApprovalHistory()
        {
            OccurredUtc = DateTime.UtcNow;
        }

        public int Id { get; set; }
        public int ClaimId { get; set; }
        public string Action { get; set; }
        public int ActorEmployeeId { get; set; }
        public string Comment { get; set; }
        public DateTime OccurredUtc { get; set; }

        public virtual ExpenseClaim Claim { get; set; }
        public virtual Employee Actor { get; set; }
    }
}
