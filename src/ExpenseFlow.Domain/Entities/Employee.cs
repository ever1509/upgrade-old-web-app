using System;
using System.Collections.Generic;
using ExpenseFlow.Domain.Enums;

namespace ExpenseFlow.Domain.Entities
{
    public class Employee
    {
        public Employee()
        {
            Claims = new List<ExpenseClaim>();
            DirectReports = new List<Employee>();
        }

        public int Id { get; set; }
        public string EmployeeNumber { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string PasswordSalt { get; set; }
        public string Role { get; set; }
        public string Department { get; set; }
        public int? ManagerId { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedUtc { get; set; }

        // Virtual = EF6 lazy loading proxies. Convenient, and the source of
        // most N+1 problems in this app. EF Core does lazy loading very
        // differently (explicit proxy package), so every one of these is an
        // assessment item.
        public virtual Employee Manager { get; set; }
        public virtual ICollection<Employee> DirectReports { get; set; }
        public virtual ICollection<ExpenseClaim> Claims { get; set; }

        public UserRole RoleValue
        {
            get
            {
                UserRole parsed;
                return Enum.TryParse(Role, true, out parsed) ? parsed : UserRole.Employee;
            }
        }

        public bool IsAdmin { get { return RoleValue == UserRole.Admin; } }
        public bool CanApprove { get { return RoleValue == UserRole.Approver || RoleValue == UserRole.Admin; } }
    }
}
