using System.Collections.Generic;
using ExpenseFlow.Domain.Entities;

namespace ExpenseFlow.Data.Repositories
{
    public interface IClaimRepository
    {
        ExpenseClaim GetById(int id);
        ExpenseClaim GetByIdWithDetails(int id);
        IList<ExpenseClaim> GetForEmployee(int employeeId);
        IList<ExpenseClaim> GetAwaitingDecisionFor(Employee approver);
        IList<ExpenseClaim> GetAll();
        string NextClaimNumber();
        void Add(ExpenseClaim claim);
        void AddLine(ExpenseLine line);
        void RemoveLine(ExpenseLine line);
        ExpenseLine GetLine(int lineId);
        void AddReceipt(Receipt receipt);
        Receipt GetReceipt(int receiptId);
        void Save();
    }
}
