using System.Data.Entity.ModelConfiguration;
using ExpenseFlow.Domain.Entities;

namespace ExpenseFlow.Data.Mappings
{
    public class ReceiptMap : EntityTypeConfiguration<Receipt>
    {
        public ReceiptMap()
        {
            ToTable("Receipts");
            HasKey(r => r.Id);

            Property(r => r.FileName).IsRequired().HasMaxLength(260);
            Property(r => r.StoredPath).IsRequired().HasMaxLength(400);
            Property(r => r.ThumbnailPath).HasMaxLength(400);
            Property(r => r.ContentType).IsRequired().HasMaxLength(100);

            HasRequired(r => r.ExpenseLine)
                .WithMany(l => l.Receipts)
                .HasForeignKey(r => r.ExpenseLineId)
                .WillCascadeOnDelete(true);

            Ignore(r => r.HasThumbnail);
        }
    }
}
