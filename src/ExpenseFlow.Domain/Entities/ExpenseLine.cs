using System;
using System.Collections.Generic;
using System.Linq;

namespace ExpenseFlow.Domain.Entities
{
    public class ExpenseLine
    {
        public ExpenseLine()
        {
            Receipts = new List<Receipt>();
            Currency = "USD";
        }

        public int Id { get; set; }
        public int ClaimId { get; set; }
        public int CategoryId { get; set; }
        public DateTime ExpenseDate { get; set; }
        public string Description { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }

        public virtual ExpenseClaim Claim { get; set; }
        public virtual ExpenseCategory Category { get; set; }
        public virtual ICollection<Receipt> Receipts { get; set; }

        public bool HasReceipt
        {
            get { return Receipts != null && Receipts.Any(); }
        }
    }
}
