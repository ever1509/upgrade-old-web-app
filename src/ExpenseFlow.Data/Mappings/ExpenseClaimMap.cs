using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using ExpenseFlow.Domain.Entities;

namespace ExpenseFlow.Data.Mappings
{
    public class ExpenseClaimMap : EntityTypeConfiguration<ExpenseClaim>
    {
        public ExpenseClaimMap()
        {
            ToTable("ExpenseClaims");
            HasKey(c => c.Id);

            Property(c => c.ClaimNumber).IsRequired().HasMaxLength(20);
            Property(c => c.Title).IsRequired().HasMaxLength(200);
            Property(c => c.Status).IsRequired().HasMaxLength(20);
            Property(c => c.TotalAmount).HasPrecision(18, 2);
            Property(c => c.RejectionReason).HasMaxLength(500);
            Property(c => c.PdfPath).HasMaxLength(400);

            Property(c => c.RowVersion)
                .IsRowVersion()
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Computed);

            HasRequired(c => c.Employee)
                .WithMany(e => e.Claims)
                .HasForeignKey(c => c.EmployeeId)
                .WillCascadeOnDelete(false);

            HasOptional(c => c.Project)
                .WithMany(p => p.Claims)
                .HasForeignKey(c => c.ProjectId)
                .WillCascadeOnDelete(false);

            HasOptional(c => c.DecidedBy)
                .WithMany()
                .HasForeignKey(c => c.DecidedByEmployeeId)
                .WillCascadeOnDelete(false);

            // Computed in code, not stored.
            Ignore(c => c.StatusValue);
            Ignore(c => c.IsEditable);
        }
    }
}
