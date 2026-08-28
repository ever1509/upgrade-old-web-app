using System.Data.Entity.ModelConfiguration;
using ExpenseFlow.Domain.Entities;

namespace ExpenseFlow.Data.Mappings
{
    public class ExpenseCategoryMap : EntityTypeConfiguration<ExpenseCategory>
    {
        public ExpenseCategoryMap()
        {
            ToTable("ExpenseCategories");
            HasKey(c => c.Id);

            Property(c => c.Code).IsRequired().HasMaxLength(20);
            Property(c => c.Name).IsRequired().HasMaxLength(100);
            Property(c => c.MaxAmountWithoutReceipt).HasPrecision(18, 2);
        }
    }
}
