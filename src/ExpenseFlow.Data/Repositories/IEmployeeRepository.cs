using System.Collections.Generic;
using ExpenseFlow.Domain.Entities;

namespace ExpenseFlow.Data.Repositories
{
    public interface IEmployeeRepository
    {
        Employee GetById(int id);
        Employee GetByEmail(string email);
        IList<Employee> GetAll();
        void Save();
    }
}
