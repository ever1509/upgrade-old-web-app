/* ============================================================
   ExpenseFlow - reporting stored procedures
   Called through EF6 SqlQuery<T>. These are a deliberate
   migration talking point: raw SQL survives EF6 -> EF Core,
   but the API changes and provider-specific T-SQL is the
   thing that leaks if you ever swap to PostgreSQL.
   ============================================================ */
USE [ExpenseFlow];
GO

IF OBJECT_ID('dbo.usp_SpendByDepartment','P') IS NOT NULL DROP PROCEDURE dbo.usp_SpendByDepartment;
GO
CREATE PROCEDURE dbo.usp_SpendByDepartment
    @FromUtc DATETIME,
    @ToUtc   DATETIME
AS
BEGIN
    SET NOCOUNT ON;

    SELECT  e.Department                      AS Department,
            COUNT(DISTINCT c.Id)              AS ClaimCount,
            ISNULL(SUM(c.TotalAmount), 0)     AS TotalAmount,
            ISNULL(AVG(c.TotalAmount), 0)     AS AverageAmount
      FROM  dbo.ExpenseClaims c
      JOIN  dbo.Employees e ON e.Id = c.EmployeeId
     WHERE  c.Status IN ('Approved', 'Reimbursed')
       AND  c.DecidedUtc >= @FromUtc
       AND  c.DecidedUtc <  @ToUtc
     GROUP BY e.Department
     ORDER BY TotalAmount DESC;
END
GO

IF OBJECT_ID('dbo.usp_SpendByCategory','P') IS NOT NULL DROP PROCEDURE dbo.usp_SpendByCategory;
GO
CREATE PROCEDURE dbo.usp_SpendByCategory
    @FromUtc DATETIME,
    @ToUtc   DATETIME
AS
BEGIN
    SET NOCOUNT ON;

    SELECT  cat.Name                          AS Category,
            COUNT(l.Id)                       AS LineCount,
            ISNULL(SUM(l.Amount), 0)          AS TotalAmount
      FROM  dbo.ExpenseLines l
      JOIN  dbo.ExpenseClaims c   ON c.Id = l.ClaimId
      JOIN  dbo.ExpenseCategories cat ON cat.Id = l.CategoryId
     WHERE  c.Status IN ('Approved', 'Reimbursed')
       AND  c.DecidedUtc >= @FromUtc
       AND  c.DecidedUtc <  @ToUtc
     GROUP BY cat.Name
     ORDER BY TotalAmount DESC;
END
GO

PRINT 'Reporting procedures created.';
GO
