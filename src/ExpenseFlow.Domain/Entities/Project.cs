using System.Collections.Generic;

namespace ExpenseFlow.Domain.Entities
{
    public class Project
    {
        public Project()
        {
            Claims = new List<ExpenseClaim>();
        }

        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; }

        public virtual ICollection<ExpenseClaim> Claims { get; set; }

        public string DisplayName { get { return Code + " - " + Name; } }
    }
}
