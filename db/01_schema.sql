/* ============================================================
   ExpenseFlow - schema (SQL Server)
   Phase 1 target: LocalDB (MSSQLLocalDB)
   Phase 4 target: SQL Server Developer Edition w/ TCP enabled
   ============================================================ */

IF DB_ID('ExpenseFlow') IS NULL
    CREATE DATABASE [ExpenseFlow];
GO

USE [ExpenseFlow];
GO

/* Drop in FK-safe order so the script is re-runnable */
IF OBJECT_ID('dbo.ApprovalHistory','U')  IS NOT NULL DROP TABLE dbo.ApprovalHistory;
IF OBJECT_ID('dbo.Receipts','U')         IS NOT NULL DROP TABLE dbo.Receipts;
IF OBJECT_ID('dbo.ExpenseLines','U')     IS NOT NULL DROP TABLE dbo.ExpenseLines;
IF OBJECT_ID('dbo.ExpenseClaims','U')    IS NOT NULL DROP TABLE dbo.ExpenseClaims;
IF OBJECT_ID('dbo.ExpenseCategories','U')IS NOT NULL DROP TABLE dbo.ExpenseCategories;
IF OBJECT_ID('dbo.Projects','U')         IS NOT NULL DROP TABLE dbo.Projects;
IF OBJECT_ID('dbo.AuditLog','U')         IS NOT NULL DROP TABLE dbo.AuditLog;
IF OBJECT_ID('dbo.Employees','U')        IS NOT NULL DROP TABLE dbo.Employees;
GO

CREATE TABLE dbo.Employees
(
    Id              INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_Employees PRIMARY KEY,
    EmployeeNumber  NVARCHAR(20)        NOT NULL,
    FullName        NVARCHAR(150)       NOT NULL,
    Email           NVARCHAR(200)       NOT NULL,
    PasswordHash    NVARCHAR(200)       NOT NULL,
    PasswordSalt    NVARCHAR(64)        NOT NULL,
    Role            NVARCHAR(20)        NOT NULL,   -- Employee | Approver | Admin
    Department      NVARCHAR(100)       NOT NULL,
    ManagerId       INT                 NULL,
    IsActive        BIT                 NOT NULL CONSTRAINT DF_Employees_IsActive DEFAULT(1),
    CreatedUtc      DATETIME            NOT NULL CONSTRAINT DF_Employees_CreatedUtc DEFAULT(GETUTCDATE()),
    CONSTRAINT UQ_Employees_EmployeeNumber UNIQUE (EmployeeNumber),
    CONSTRAINT UQ_Employees_Email          UNIQUE (Email),
    CONSTRAINT FK_Employees_Manager        FOREIGN KEY (ManagerId) REFERENCES dbo.Employees(Id)
);
GO

CREATE TABLE dbo.Projects
(
    Id        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Projects PRIMARY KEY,
    Code      NVARCHAR(20)      NOT NULL,
    Name      NVARCHAR(150)     NOT NULL,
    IsActive  BIT               NOT NULL CONSTRAINT DF_Projects_IsActive DEFAULT(1),
    CONSTRAINT UQ_Projects_Code UNIQUE (Code)
);
GO

CREATE TABLE dbo.ExpenseCategories
(
    Id                        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ExpenseCategories PRIMARY KEY,
    Code                      NVARCHAR(20)      NOT NULL,
    Name                      NVARCHAR(100)     NOT NULL,
    RequiresReceipt           BIT               NOT NULL CONSTRAINT DF_Cat_RequiresReceipt DEFAULT(1),
    MaxAmountWithoutReceipt   DECIMAL(18,2)     NOT NULL CONSTRAINT DF_Cat_MaxNoReceipt DEFAULT(0),
    IsActive                  BIT               NOT NULL CONSTRAINT DF_Cat_IsActive DEFAULT(1),
    CONSTRAINT UQ_ExpenseCategories_Code UNIQUE (Code)
);
GO

CREATE TABLE dbo.ExpenseClaims
(
    Id                   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ExpenseClaims PRIMARY KEY,
    ClaimNumber          NVARCHAR(20)      NOT NULL,
    EmployeeId           INT               NOT NULL,
    ProjectId            INT               NULL,
    Title                NVARCHAR(200)     NOT NULL,
    Status               NVARCHAR(20)      NOT NULL,   -- Draft|Submitted|Approved|Rejected|Reimbursed
    TotalAmount          DECIMAL(18,2)     NOT NULL CONSTRAINT DF_Claims_Total DEFAULT(0),
    SubmittedUtc         DATETIME          NULL,
    DecidedUtc           DATETIME          NULL,
    DecidedByEmployeeId  INT               NULL,
    RejectionReason      NVARCHAR(500)     NULL,
    PdfPath              NVARCHAR(400)     NULL,
    CreatedUtc           DATETIME          NOT NULL CONSTRAINT DF_Claims_CreatedUtc DEFAULT(GETUTCDATE()),
    RowVersion           ROWVERSION        NOT NULL,
    CONSTRAINT UQ_ExpenseClaims_ClaimNumber UNIQUE (ClaimNumber),
    CONSTRAINT FK_Claims_Employee  FOREIGN KEY (EmployeeId)          REFERENCES dbo.Employees(Id),
    CONSTRAINT FK_Claims_Project   FOREIGN KEY (ProjectId)           REFERENCES dbo.Projects(Id),
    CONSTRAINT FK_Claims_DecidedBy FOREIGN KEY (DecidedByEmployeeId) REFERENCES dbo.Employees(Id)
);
GO
CREATE INDEX IX_ExpenseClaims_Employee_Status ON dbo.ExpenseClaims(EmployeeId, Status);
CREATE INDEX IX_ExpenseClaims_Status          ON dbo.ExpenseClaims(Status);
GO

CREATE TABLE dbo.ExpenseLines
(
    Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ExpenseLines PRIMARY KEY,
    ClaimId      INT               NOT NULL,
    CategoryId   INT               NOT NULL,
    ExpenseDate  DATETIME          NOT NULL,
    Description  NVARCHAR(300)     NOT NULL,
    Amount       DECIMAL(18,2)     NOT NULL,
    Currency     NVARCHAR(3)       NOT NULL CONSTRAINT DF_Lines_Currency DEFAULT('USD'),
    CONSTRAINT FK_Lines_Claim    FOREIGN KEY (ClaimId)    REFERENCES dbo.ExpenseClaims(Id) ON DELETE CASCADE,
    CONSTRAINT FK_Lines_Category FOREIGN KEY (CategoryId) REFERENCES dbo.ExpenseCategories(Id)
);
GO
CREATE INDEX IX_ExpenseLines_Claim ON dbo.ExpenseLines(ClaimId);
GO

CREATE TABLE dbo.Receipts
(
    Id             INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Receipts PRIMARY KEY,
    ExpenseLineId  INT               NOT NULL,
    FileName       NVARCHAR(260)     NOT NULL,
    StoredPath     NVARCHAR(400)     NOT NULL,
    ThumbnailPath  NVARCHAR(400)     NULL,
    ContentType    NVARCHAR(100)     NOT NULL,
    SizeBytes      BIGINT            NOT NULL,
    UploadedUtc    DATETIME          NOT NULL CONSTRAINT DF_Receipts_UploadedUtc DEFAULT(GETUTCDATE()),
    CONSTRAINT FK_Receipts_Line FOREIGN KEY (ExpenseLineId) REFERENCES dbo.ExpenseLines(Id) ON DELETE CASCADE
);
GO
CREATE INDEX IX_Receipts_Line ON dbo.Receipts(ExpenseLineId);
GO

CREATE TABLE dbo.ApprovalHistory
(
    Id               INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ApprovalHistory PRIMARY KEY,
    ClaimId          INT               NOT NULL,
    Action           NVARCHAR(30)      NOT NULL,   -- Created|Submitted|Approved|Rejected|Reimbursed|Resubmitted
    ActorEmployeeId  INT               NOT NULL,
    Comment          NVARCHAR(500)     NULL,
    OccurredUtc      DATETIME          NOT NULL CONSTRAINT DF_History_OccurredUtc DEFAULT(GETUTCDATE()),
    CONSTRAINT FK_History_Claim FOREIGN KEY (ClaimId)         REFERENCES dbo.ExpenseClaims(Id) ON DELETE CASCADE,
    CONSTRAINT FK_History_Actor FOREIGN KEY (ActorEmployeeId) REFERENCES dbo.Employees(Id)
);
GO
CREATE INDEX IX_ApprovalHistory_Claim ON dbo.ApprovalHistory(ClaimId);
GO

/* Written by the IHttpModule on every request. Deliberately chatty legacy pattern. */
CREATE TABLE dbo.AuditLog
(
    Id          BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditLog PRIMARY KEY,
    OccurredUtc DATETIME       NOT NULL CONSTRAINT DF_Audit_OccurredUtc DEFAULT(GETUTCDATE()),
    UserName    NVARCHAR(200)  NULL,
    HttpMethod  NVARCHAR(10)   NOT NULL,
    Path        NVARCHAR(400)  NOT NULL,
    StatusCode  INT            NOT NULL,
    DurationMs  INT            NOT NULL
);
GO
