using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using ExpenseFlow.Domain.Entities;

namespace ExpenseFlow.Data.Repositories
{
    public class EmployeeRepository : IEmployeeRepository
    {
        private readonly ExpenseFlowContext _db;

        public EmployeeRepository(ExpenseFlowContext db)
        {
            if (db == null) throw new ArgumentNullException("db");
            _db = db;
        }

        public Employee GetById(int id)
        {
            return _db.Employees.Include(e => e.Manager).FirstOrDefault(e => e.Id == id);
        }

        public Employee GetByEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;
            var normalized = email.Trim();
            return _db.Employees
                      .Include(e => e.Manager)
                      .FirstOrDefault(e => e.Email == normalized && e.IsActive);
        }

        public IList<Employee> GetAll()
        {
            return _db.Employees.OrderBy(e => e.FullName).ToList();
        }

        public void Save() { _db.SaveChanges(); }
    }
}
