using System.Data.Entity.ModelConfiguration;
using ExpenseFlow.Domain.Entities;

namespace ExpenseFlow.Data.Mappings
{
    public class AuditLogEntryMap : EntityTypeConfiguration<AuditLogEntry>
    {
        public AuditLogEntryMap()
        {
            ToTable("AuditLog");
            HasKey(a => a.Id);

            Property(a => a.UserName).HasMaxLength(200);
            Property(a => a.HttpMethod).IsRequired().HasMaxLength(10);
            Property(a => a.Path).IsRequired().HasMaxLength(400);
        }
    }
}
