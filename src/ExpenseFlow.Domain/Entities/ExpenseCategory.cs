using System.Collections.Generic;

namespace ExpenseFlow.Domain.Entities
{
    public class ExpenseCategory
    {
        public ExpenseCategory()
        {
            Lines = new List<ExpenseLine>();
        }

        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public bool RequiresReceipt { get; set; }
        public decimal MaxAmountWithoutReceipt { get; set; }
        public bool IsActive { get; set; }

        public virtual ICollection<ExpenseLine> Lines { get; set; }
    }
}
