using System;
using ExpenseFlow.Domain.Entities;
using ExpenseFlow.Domain.Enums;

namespace ExpenseFlow.Tests.Support
{
    /// <summary>
    /// Object mothers. Every test reads as a sentence about a business rule
    /// rather than a wall of property assignments - which matters, because
    /// after the migration these same tests must still be readable enough to
    /// tell you WHAT broke, not just that something did.
    /// </summary>
    public static class TestData
    {
        // ---- categories (mirroring db/02_seed.sql) ----

        public static ExpenseCategory Meals()
        {
            return new ExpenseCategory
            {
                Id = 1, Code = "MEALS", Name = "Meals",
                RequiresReceipt = true, MaxAmountWithoutReceipt = 25m, IsActive = true
            };
        }

        public static ExpenseCategory Taxi()
        {
            return new ExpenseCategory
            {
                Id = 2, Code = "TAXI", Name = "Taxi / Rideshare",
                RequiresReceipt = true, MaxAmountWithoutReceipt = 15m, IsActive = true
            };
        }

        /// <summary>Always needs a receipt, whatever the amount.</summary>
        public static ExpenseCategory Travel()
        {
            return new ExpenseCategory
            {
                Id = 3, Code = "TRAVEL", Name = "Air / Rail Travel",
                RequiresReceipt = true, MaxAmountWithoutReceipt = 0m, IsActive = true
            };
        }

        /// <summary>Never needs a receipt.</summary>
        public static ExpenseCategory Mileage()
        {
            return new ExpenseCategory
            {
                Id = 4, Code = "MILEAGE", Name = "Personal Car Mileage",
                RequiresReceipt = false, MaxAmountWithoutReceipt = 0m, IsActive = true
            };
        }

        // ---- people ----

        public static Employee Employee(int id = 1, int? managerId = 2)
        {
            return Person(id, "Employee", managerId);
        }

        public static Employee Approver(int id = 2, int? managerId = 4)
        {
            return Person(id, "Approver", managerId);
        }

        public static Employee Admin(int id = 4)
        {
            return Person(id, "Admin", null);
        }

        public static Employee Person(int id, string role, int? managerId)
        {
            return new Employee
            {
                Id = id,
                EmployeeNumber = "EMP-" + id.ToString("D3"),
                FullName = "Person " + id,
                Email = "person" + id + "@expenseflow.local",
                Role = role,
                Department = "Engineering",
                ManagerId = managerId,
                IsActive = true,
                CreatedUtc = DateTime.UtcNow
            };
        }

        // ---- claims ----

        public static ExpenseClaim Claim(Employee owner, ClaimStatus status = ClaimStatus.Draft, string title = "Client workshop")
        {
            var claim = new ExpenseClaim
            {
                Id = 100,
                ClaimNumber = "CLM-000100",
                EmployeeId = owner.Id,
                Employee = owner,
                Title = title
            };
            claim.StatusValue = status;
            return claim;
        }

        public static ExpenseLine Line(ExpenseCategory category, decimal amount,
                                       int daysAgo = 3, bool withReceipt = false)
        {
            var line = new ExpenseLine
            {
                Id = _nextLineId++,
                CategoryId = category.Id,
                Category = category,
                Amount = amount,
                Currency = "USD",
                Description = "Line for " + category.Code,
                ExpenseDate = DateTime.UtcNow.Date.AddDays(-daysAgo)
            };

            if (withReceipt) line.Receipts.Add(Receipt());
            return line;
        }

        public static Receipt Receipt()
        {
            return new Receipt
            {
                Id = _nextReceiptId++,
                FileName = "receipt.jpg",
                StoredPath = "100/receipt.jpg",
                ContentType = "image/jpeg",
                SizeBytes = 2048,
                UploadedUtc = DateTime.UtcNow
            };
        }

        /// <summary>A claim that satisfies every submit rule.</summary>
        public static ExpenseClaim SubmittableClaim(Employee owner)
        {
            var claim = Claim(owner);
            claim.Lines.Add(Line(Meals(), 18.40m));
            claim.Lines.Add(Line(Taxi(), 12.75m));
            return claim;
        }

        private static int _nextLineId = 1;
        private static int _nextReceiptId = 1;
    }
}
