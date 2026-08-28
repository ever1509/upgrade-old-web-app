using System.Data.Entity.ModelConfiguration;
using ExpenseFlow.Domain.Entities;

namespace ExpenseFlow.Data.Mappings
{
    public class ApprovalHistoryMap : EntityTypeConfiguration<ApprovalHistory>
    {
        public ApprovalHistoryMap()
        {
            ToTable("ApprovalHistory");
            HasKey(h => h.Id);

            Property(h => h.Action).IsRequired().HasMaxLength(30);
            Property(h => h.Comment).HasMaxLength(500);

            HasRequired(h => h.Claim)
                .WithMany(c => c.History)
                .HasForeignKey(h => h.ClaimId)
                .WillCascadeOnDelete(true);

            HasRequired(h => h.Actor)
                .WithMany()
                .HasForeignKey(h => h.ActorEmployeeId)
                .WillCascadeOnDelete(false);
        }
    }
}
