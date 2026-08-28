using System.Collections.Generic;
using ExpenseFlow.Domain.Entities;

namespace ExpenseFlow.Data.Repositories
{
    public interface ILookupRepository
    {
        IList<Project> ActiveProjects();
        IList<ExpenseCategory> ActiveCategories();
        ExpenseCategory GetCategory(int id);
    }
}
