/* ============================================================
   ExpenseFlow - seed data
   All demo users share the password:  Passw0rd!
   Hash scheme: Base64( SHA256( UTF8(salt + password) ) )
   NOTE: deliberately weak/legacy. It is a finding you will
   record during the migration assessment (phase 3).
   ============================================================ */
USE [ExpenseFlow];
GO

DELETE FROM dbo.ApprovalHistory;
DELETE FROM dbo.Receipts;
DELETE FROM dbo.ExpenseLines;
DELETE FROM dbo.ExpenseClaims;
DELETE FROM dbo.AuditLog;
DELETE FROM dbo.ExpenseCategories;
DELETE FROM dbo.Projects;
DELETE FROM dbo.Employees;
GO

/* --- Employees (insert order matters: managers before reports) --- */
INSERT INTO dbo.Employees (EmployeeNumber, FullName, Email, PasswordHash, PasswordSalt, Role, Department, ManagerId)
VALUES ('EMP-004', 'Dana Okafor', 'dana@expenseflow.local',
        'CliBusE+MPUOJ6Fm8LOi4edQqs7f8l/i3TMUXNjBcXg=', 's1dana01', 'Admin', 'Finance', NULL);

INSERT INTO dbo.Employees (EmployeeNumber, FullName, Email, PasswordHash, PasswordSalt, Role, Department, ManagerId)
VALUES ('EMP-002', 'Bob Chen', 'bob@expenseflow.local',
        'HeISA75mpsfplFmYf7O80nyM6lGM114IZNsoh1I3fRE=', 's2bob002', 'Approver', 'Engineering',
        (SELECT Id FROM dbo.Employees WHERE EmployeeNumber = 'EMP-004'));

INSERT INTO dbo.Employees (EmployeeNumber, FullName, Email, PasswordHash, PasswordSalt, Role, Department, ManagerId)
VALUES ('EMP-001', 'Alice Moreno', 'alice@expenseflow.local',
        'fqVJQZ5AmJje/ABvAxFREWYXKEY1qVihU4tJClxM4qY=', 's3alice3', 'Employee', 'Engineering',
        (SELECT Id FROM dbo.Employees WHERE EmployeeNumber = 'EMP-002'));

INSERT INTO dbo.Employees (EmployeeNumber, FullName, Email, PasswordHash, PasswordSalt, Role, Department, ManagerId)
VALUES ('EMP-003', 'Carla Ruiz', 'carla@expenseflow.local',
        'k8+jJ+XGF4OqkaWx10GCGOfgrCMAQuAYeM5RKaqkYRw=', 's4carla4', 'Employee', 'Sales',
        (SELECT Id FROM dbo.Employees WHERE EmployeeNumber = 'EMP-002'));

INSERT INTO dbo.Employees (EmployeeNumber, FullName, Email, PasswordHash, PasswordSalt, Role, Department, ManagerId)
VALUES ('EMP-005', 'Erik Lindqvist', 'erik@expenseflow.local',
        '9W6SIZRAiiahDnZJ5KCWrHh0vpcmLO/7yvqhKy669Qw=', 's5erik05', 'Employee', 'Engineering',
        (SELECT Id FROM dbo.Employees WHERE EmployeeNumber = 'EMP-002'));
GO

/* --- Projects --- */
INSERT INTO dbo.Projects (Code, Name) VALUES
 ('PRJ-APOLLO',  'Apollo Platform Rebuild'),
 ('PRJ-ATLAS',   'Atlas Data Migration'),
 ('PRJ-INTERNAL','Internal / Non-billable');
GO

/* --- Categories --- */
INSERT INTO dbo.ExpenseCategories (Code, Name, RequiresReceipt, MaxAmountWithoutReceipt) VALUES
 ('TRAVEL',  'Air / Rail Travel',   1,   0.00),
 ('HOTEL',   'Accommodation',       1,   0.00),
 ('MEALS',   'Meals',               1,  25.00),
 ('TAXI',    'Taxi / Rideshare',    1,  15.00),
 ('SUPPLIES','Office Supplies',     1,  50.00),
 ('MILEAGE', 'Personal Car Mileage',0,   0.00),
 ('TRAINING','Training & Books',    1,   0.00);
GO

/* --- One claim already in Draft so there is something to click on --- */
DECLARE @alice INT = (SELECT Id FROM dbo.Employees WHERE EmployeeNumber = 'EMP-001');
DECLARE @apollo INT = (SELECT Id FROM dbo.Projects WHERE Code = 'PRJ-APOLLO');
DECLARE @meals INT = (SELECT Id FROM dbo.ExpenseCategories WHERE Code = 'MEALS');
DECLARE @taxi  INT = (SELECT Id FROM dbo.ExpenseCategories WHERE Code = 'TAXI');

INSERT INTO dbo.ExpenseClaims (ClaimNumber, EmployeeId, ProjectId, Title, Status, TotalAmount)
VALUES ('CLM-000001', @alice, @apollo, 'Client workshop - Berlin', 'Draft', 0);

DECLARE @claim INT = SCOPE_IDENTITY();

INSERT INTO dbo.ExpenseLines (ClaimId, CategoryId, ExpenseDate, Description, Amount, Currency) VALUES
 (@claim, @meals, DATEADD(day, -6, GETUTCDATE()), 'Team dinner after workshop', 18.40, 'USD'),
 (@claim, @taxi,  DATEADD(day, -6, GETUTCDATE()), 'Airport to hotel',            12.75, 'USD');

UPDATE dbo.ExpenseClaims
   SET TotalAmount = (SELECT SUM(Amount) FROM dbo.ExpenseLines WHERE ClaimId = @claim)
 WHERE Id = @claim;

INSERT INTO dbo.ApprovalHistory (ClaimId, Action, ActorEmployeeId, Comment)
VALUES (@claim, 'Created', @alice, 'Seeded draft claim');
GO

PRINT 'Seed complete. Login with alice@expenseflow.local / Passw0rd!';
GO
