using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using ExpenseFlow.Domain.Entities;
using ExpenseFlow.Domain.Enums;

namespace ExpenseFlow.Data.Repositories
{
    public class ClaimRepository : IClaimRepository
    {
        private readonly ExpenseFlowContext _db;

        public ClaimRepository(ExpenseFlowContext db)
        {
            if (db == null) throw new ArgumentNullException("db");
            _db = db;
        }

        public ExpenseClaim GetById(int id)
        {
            return _db.Claims.FirstOrDefault(c => c.Id == id);
        }

        public ExpenseClaim GetByIdWithDetails(int id)
        {
            return _db.Claims
                      .Include(c => c.Employee)
                      .Include(c => c.Project)
                      .Include(c => c.DecidedBy)
                      .Include(c => c.Lines.Select(l => l.Category))
                      .Include(c => c.Lines.Select(l => l.Receipts))
                      .Include(c => c.History.Select(h => h.Actor))
                      .FirstOrDefault(c => c.Id == id);
        }

        public IList<ExpenseClaim> GetForEmployee(int employeeId)
        {
            return _db.Claims
                      .Include(c => c.Project)
                      .Where(c => c.EmployeeId == employeeId)
                      .OrderByDescending(c => c.CreatedUtc)
                      .ToList();
        }

        public IList<ExpenseClaim> GetAwaitingDecisionFor(Employee approver)
        {
            var submitted = ClaimStatus.Submitted.ToString();

            var query = _db.Claims
                           .Include(c => c.Employee)
                           .Include(c => c.Project)
                           .Where(c => c.Status == submitted);

            // Admins see everything; approvers only their own direct reports.
            if (!approver.IsAdmin)
            {
                var approverId = approver.Id;
                query = query.Where(c => c.Employee.ManagerId == approverId);
            }

            return query.OrderBy(c => c.SubmittedUtc).ToList();
        }

        public IList<ExpenseClaim> GetAll()
        {
            return _db.Claims
                      .Include(c => c.Employee)
                      .Include(c => c.Project)
                      .OrderByDescending(c => c.CreatedUtc)
                      .ToList();
        }

        /// <summary>
        /// MAX+1. Racy under concurrency - a real finding for the assessment,
        /// and a nice thing to fix with a sequence after the migration.
        /// </summary>
        public string NextClaimNumber()
        {
            var last = _db.Claims
                          .OrderByDescending(c => c.Id)
                          .Select(c => c.ClaimNumber)
                          .FirstOrDefault();

            var next = 1;
            if (!string.IsNullOrEmpty(last))
            {
                var digits = last.Replace("CLM-", string.Empty);
                int parsed;
                if (int.TryParse(digits, out parsed)) next = parsed + 1;
            }

            return "CLM-" + next.ToString("D6");
        }

        public void Add(ExpenseClaim claim)      { _db.Claims.Add(claim); }
        public void AddLine(ExpenseLine line)    { _db.Lines.Add(line); }
        public void RemoveLine(ExpenseLine line) { _db.Lines.Remove(line); }
        public void AddReceipt(Receipt receipt)  { _db.Receipts.Add(receipt); }

        public ExpenseLine GetLine(int lineId)
        {
            return _db.Lines
                      .Include(l => l.Claim)
                      .Include(l => l.Category)
                      .Include(l => l.Receipts)
                      .FirstOrDefault(l => l.Id == lineId);
        }

        public Receipt GetReceipt(int receiptId)
        {
            return _db.Receipts.FirstOrDefault(r => r.Id == receiptId);
        }

        public void Save() { _db.SaveChanges(); }
    }
}
