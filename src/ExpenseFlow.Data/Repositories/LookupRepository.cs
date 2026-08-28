using System;
using System.Collections.Generic;
using System.Linq;
using ExpenseFlow.Domain.Entities;

namespace ExpenseFlow.Data.Repositories
{
    public class LookupRepository : ILookupRepository
    {
        private readonly ExpenseFlowContext _db;

        public LookupRepository(ExpenseFlowContext db)
        {
            if (db == null) throw new ArgumentNullException("db");
            _db = db;
        }

        public IList<Project> ActiveProjects()
        {
            return _db.Projects.Where(p => p.IsActive).OrderBy(p => p.Code).ToList();
        }

        public IList<ExpenseCategory> ActiveCategories()
        {
            return _db.Categories.Where(c => c.IsActive).OrderBy(c => c.Name).ToList();
        }

        public ExpenseCategory GetCategory(int id)
        {
            return _db.Categories.FirstOrDefault(c => c.Id == id);
        }
    }
}
