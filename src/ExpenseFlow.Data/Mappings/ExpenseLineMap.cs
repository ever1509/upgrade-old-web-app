using System.Data.Entity.ModelConfiguration;
using ExpenseFlow.Domain.Entities;

namespace ExpenseFlow.Data.Mappings
{
    public class ExpenseLineMap : EntityTypeConfiguration<ExpenseLine>
    {
        public ExpenseLineMap()
        {
            ToTable("ExpenseLines");
            HasKey(l => l.Id);

            Property(l => l.Description).IsRequired().HasMaxLength(300);
            Property(l => l.Amount).HasPrecision(18, 2);
            Property(l => l.Currency).IsRequired().HasMaxLength(3);

            HasRequired(l => l.Claim)
                .WithMany(c => c.Lines)
                .HasForeignKey(l => l.ClaimId)
                .WillCascadeOnDelete(true);

            HasRequired(l => l.Category)
                .WithMany(c => c.Lines)
                .HasForeignKey(l => l.CategoryId)
                .WillCascadeOnDelete(false);

            Ignore(l => l.HasReceipt);
        }
    }
}
