using System.Data.Entity.ModelConfiguration;
using ExpenseFlow.Domain.Entities;

namespace ExpenseFlow.Data.Mappings
{
    public class EmployeeMap : EntityTypeConfiguration<Employee>
    {
        public EmployeeMap()
        {
            ToTable("Employees");
            HasKey(e => e.Id);

            Property(e => e.EmployeeNumber).IsRequired().HasMaxLength(20);
            Property(e => e.FullName).IsRequired().HasMaxLength(150);
            Property(e => e.Email).IsRequired().HasMaxLength(200);
            Property(e => e.PasswordHash).IsRequired().HasMaxLength(200);
            Property(e => e.PasswordSalt).IsRequired().HasMaxLength(64);
            Property(e => e.Role).IsRequired().HasMaxLength(20);
            Property(e => e.Department).IsRequired().HasMaxLength(100);

            HasOptional(e => e.Manager)
                .WithMany(m => m.DirectReports)
                .HasForeignKey(e => e.ManagerId)
                .WillCascadeOnDelete(false);

            // Computed in code, never persisted.
            Ignore(e => e.RoleValue);
            Ignore(e => e.IsAdmin);
            Ignore(e => e.CanApprove);
        }
    }
}
