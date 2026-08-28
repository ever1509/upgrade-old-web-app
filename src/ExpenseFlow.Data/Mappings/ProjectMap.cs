using System.Data.Entity.ModelConfiguration;
using ExpenseFlow.Domain.Entities;

namespace ExpenseFlow.Data.Mappings
{
    public class ProjectMap : EntityTypeConfiguration<Project>
    {
        public ProjectMap()
        {
            ToTable("Projects");
            HasKey(p => p.Id);

            Property(p => p.Code).IsRequired().HasMaxLength(20);
            Property(p => p.Name).IsRequired().HasMaxLength(150);

            Ignore(p => p.DisplayName);
        }
    }
}
