using System.Data.Entity;
using ExpenseFlow.Data.Mappings;
using ExpenseFlow.Domain.Entities;

namespace ExpenseFlow.Data
{
    /// <summary>
    /// EF6 code-first mapped onto an existing, hand-written schema.
    /// The initializer is disabled: the database is owned by db/*.sql,
    /// not by the ORM, which is how most real LOB apps of this era work.
    /// </summary>
    public class ExpenseFlowContext : DbContext
    {
        public ExpenseFlowContext() : base("name=ExpenseFlow")
        {
            // Lazy loading on. Convenient, and the reason half the views
            // issue N+1 queries. EF Core needs an explicit proxy package.
            Configuration.LazyLoadingEnabled = true;
            Configuration.ProxyCreationEnabled = true;
        }

        public ExpenseFlowContext(string nameOrConnectionString) : base(nameOrConnectionString)
        {
            Configuration.LazyLoadingEnabled = true;
            Configuration.ProxyCreationEnabled = true;
        }

        static ExpenseFlowContext()
        {
            Database.SetInitializer<ExpenseFlowContext>(null);
        }

        public virtual DbSet<Employee> Employees { get; set; }
        public virtual DbSet<Project> Projects { get; set; }
        public virtual DbSet<ExpenseCategory> Categories { get; set; }
        public virtual DbSet<ExpenseClaim> Claims { get; set; }
        public virtual DbSet<ExpenseLine> Lines { get; set; }
        public virtual DbSet<Receipt> Receipts { get; set; }
        public virtual DbSet<ApprovalHistory> ApprovalHistory { get; set; }
        public virtual DbSet<AuditLogEntry> AuditLog { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Configurations.Add(new EmployeeMap());
            modelBuilder.Configurations.Add(new ProjectMap());
            modelBuilder.Configurations.Add(new ExpenseCategoryMap());
            modelBuilder.Configurations.Add(new ExpenseClaimMap());
            modelBuilder.Configurations.Add(new ExpenseLineMap());
            modelBuilder.Configurations.Add(new ReceiptMap());
            modelBuilder.Configurations.Add(new ApprovalHistoryMap());
            modelBuilder.Configurations.Add(new AuditLogEntryMap());

            base.OnModelCreating(modelBuilder);
        }
    }
}
