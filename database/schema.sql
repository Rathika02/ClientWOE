/* ============================================================================
   Client WOE Entry - SQL Server schema + seed data
   Creates database ClientWOE_DB with the same structure EF Core produces:
     Client, Patient, TestMaster, WorkOrder, WorkOrderTestDetail
   Safe to re-run: every object is created only if it does not exist yet.

   Use this script INSTEAD of EF migrations (not in addition to them).
   ============================================================================ */

IF DB_ID(N'ClientWOE_DB') IS NULL
    CREATE DATABASE ClientWOE_DB;
GO

USE ClientWOE_DB;
GO

/* ------------------------------------------------------------------ Client */
IF OBJECT_ID(N'dbo.Client', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Client
    (
        ClientId    INT IDENTITY(1,1) NOT NULL,
        ClientCode  NVARCHAR(20)      NOT NULL,
        ClientName  NVARCHAR(150)     NOT NULL,
        Phone       NVARCHAR(20)      NULL,
        Address     NVARCHAR(300)     NULL,
        IsActive    BIT               NOT NULL CONSTRAINT DF_Client_IsActive  DEFAULT (1),
        CreatedAt   DATETIME2         NOT NULL CONSTRAINT DF_Client_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_Client PRIMARY KEY (ClientId)
    );
    CREATE UNIQUE INDEX IX_Client_ClientCode ON dbo.Client (ClientCode);
END
GO

/* ----------------------------------------------------------------- Patient */
IF OBJECT_ID(N'dbo.Patient', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Patient
    (
        PatientId     INT IDENTITY(1,1) NOT NULL,
        PatientCode   NVARCHAR(20)      NOT NULL,
        PatientName   NVARCHAR(100)     NOT NULL,
        Age           INT               NOT NULL,
        Gender        INT               NOT NULL,      -- 0 = Male, 1 = Female, 2 = Other
        MobileNumber  NVARCHAR(15)      NOT NULL,
        ClientId      INT               NOT NULL,
        CreatedAt     DATETIME2         NOT NULL CONSTRAINT DF_Patient_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_Patient PRIMARY KEY (PatientId),
        CONSTRAINT FK_Patient_Client FOREIGN KEY (ClientId) REFERENCES dbo.Client (ClientId),
        CONSTRAINT CK_Patient_Age    CHECK (Age BETWEEN 0 AND 120),
        CONSTRAINT CK_Patient_Gender CHECK (Gender IN (0, 1, 2))
    );
    CREATE UNIQUE INDEX IX_Patient_PatientCode  ON dbo.Patient (PatientCode);
    CREATE INDEX        IX_Patient_MobileNumber ON dbo.Patient (MobileNumber);
    CREATE INDEX        IX_Patient_ClientId     ON dbo.Patient (ClientId);
END
GO

/* -------------------------------------------------------------- TestMaster */
IF OBJECT_ID(N'dbo.TestMaster', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TestMaster
    (
        TestId    INT IDENTITY(1,1) NOT NULL,
        TestCode  NVARCHAR(20)      NOT NULL,
        TestName  NVARCHAR(150)     NOT NULL,
        Category  NVARCHAR(50)      NULL,
        Rate      DECIMAL(18,2)     NOT NULL,
        IsActive  BIT               NOT NULL CONSTRAINT DF_TestMaster_IsActive DEFAULT (1),
        CONSTRAINT PK_TestMaster PRIMARY KEY (TestId),
        CONSTRAINT CK_TestMaster_Rate CHECK (Rate > 0)
    );
    CREATE UNIQUE INDEX IX_TestMaster_TestCode ON dbo.TestMaster (TestCode);
END
GO

/* --------------------------------------------------------------- WorkOrder */
IF OBJECT_ID(N'dbo.WorkOrder', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorkOrder
    (
        WorkOrderId  INT IDENTITY(1,1) NOT NULL,
        WoeNumber    NVARCHAR(30)      NOT NULL,       -- WOE-yyyyMMdd-####
        OrderDate    DATETIME2         NOT NULL CONSTRAINT DF_WorkOrder_OrderDate DEFAULT (SYSUTCDATETIME()),
        ClientId     INT               NOT NULL,
        PatientId    INT               NOT NULL,
        TotalAmount  DECIMAL(18,2)     NOT NULL,
        Status       NVARCHAR(20)      NOT NULL CONSTRAINT DF_WorkOrder_Status DEFAULT (N'Saved'),
        CONSTRAINT PK_WorkOrder PRIMARY KEY (WorkOrderId),
        CONSTRAINT FK_WorkOrder_Client  FOREIGN KEY (ClientId)  REFERENCES dbo.Client  (ClientId),
        CONSTRAINT FK_WorkOrder_Patient FOREIGN KEY (PatientId) REFERENCES dbo.Patient (PatientId)
    );
    CREATE UNIQUE INDEX IX_WorkOrder_WoeNumber ON dbo.WorkOrder (WoeNumber);
    CREATE INDEX        IX_WorkOrder_OrderDate ON dbo.WorkOrder (OrderDate);
    CREATE INDEX        IX_WorkOrder_ClientId  ON dbo.WorkOrder (ClientId);
    CREATE INDEX        IX_WorkOrder_PatientId ON dbo.WorkOrder (PatientId);
END
GO

/* ------------------------------------------------------ WorkOrderTestDetail */
IF OBJECT_ID(N'dbo.WorkOrderTestDetail', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorkOrderTestDetail
    (
        WorkOrderTestDetailId  INT IDENTITY(1,1) NOT NULL,
        WorkOrderId            INT               NOT NULL,
        TestId                 INT               NOT NULL,
        Qty                    INT               NOT NULL,
        Rate                   DECIMAL(18,2)     NOT NULL,   -- copied from TestMaster at order time
        Amount                 DECIMAL(18,2)     NOT NULL,   -- Rate * Qty
        CONSTRAINT PK_WorkOrderTestDetail PRIMARY KEY (WorkOrderTestDetailId),
        CONSTRAINT FK_WorkOrderTestDetail_WorkOrder FOREIGN KEY (WorkOrderId) REFERENCES dbo.WorkOrder (WorkOrderId) ON DELETE CASCADE,
        CONSTRAINT FK_WorkOrderTestDetail_TestMaster FOREIGN KEY (TestId) REFERENCES dbo.TestMaster (TestId),
        CONSTRAINT CK_WorkOrderTestDetail_Qty CHECK (Qty > 0)
    );
    CREATE INDEX IX_WorkOrderTestDetail_WorkOrderId ON dbo.WorkOrderTestDetail (WorkOrderId);
    CREATE INDEX IX_WorkOrderTestDetail_TestId      ON dbo.WorkOrderTestDetail (TestId);
END
GO

/* ---------------------------------------------------------------- Seed: Client */
IF NOT EXISTS (SELECT 1 FROM dbo.Client)
BEGIN
    SET IDENTITY_INSERT dbo.Client ON;
    INSERT INTO dbo.Client (ClientId, ClientCode, ClientName, Phone, Address, IsActive, CreatedAt) VALUES
        (1, N'CL-WALKIN',  N'Walk-in Client',                  NULL, NULL,                          1, '2026-01-01T00:00:00'),
        (2, N'CL-APOLLO',  N'Apollo Diagnostics',              NULL, N'Bangalore, Karnataka',       1, '2026-01-01T00:00:00'),
        (3, N'CL-SUNRISE', N'Sunrise Multispeciality Hospital',NULL, N'Bangalore, Karnataka',       1, '2026-01-01T00:00:00'),
        (4, N'CL-MEDLIFE', N'MedLife Health Clinic',           NULL, N'Chennai, Tamil Nadu',        1, '2026-01-01T00:00:00'),
        (5, N'CL-GREENX',  N'Green Cross Polyclinic',          NULL, N'Coimbatore, Tamil Nadu',     1, '2026-01-01T00:00:00');
    SET IDENTITY_INSERT dbo.Client OFF;
END
GO

/* ------------------------------------------------------------- Seed: TestMaster */
IF NOT EXISTS (SELECT 1 FROM dbo.TestMaster)
BEGIN
    SET IDENTITY_INSERT dbo.TestMaster ON;
    INSERT INTO dbo.TestMaster (TestId, TestCode, TestName, Category, Rate, IsActive) VALUES
        ( 1, N'CBC',     N'Complete Blood Count',             N'Haematology',   350.00, 1),
        ( 2, N'LIPID',   N'Lipid Profile',                    N'Biochemistry',  600.00, 1),
        ( 3, N'LFT',     N'Liver Function Test',              N'Biochemistry',  700.00, 1),
        ( 4, N'KFT',     N'Kidney Function Test',             N'Biochemistry',  650.00, 1),
        ( 5, N'THYROID', N'Thyroid Profile (T3, T4, TSH)',    N'Endocrinology', 550.00, 1),
        ( 6, N'HBA1C',   N'HbA1c',                            N'Diabetes',      450.00, 1),
        ( 7, N'FBS',     N'Fasting Blood Sugar',              N'Diabetes',      120.00, 1),
        ( 8, N'PPBS',    N'Post-prandial Blood Sugar',        N'Diabetes',      120.00, 1),
        ( 9, N'VITD',    N'Vitamin D (25-OH)',                N'Vitamins',     1200.00, 1),
        (10, N'VITB12',  N'Vitamin B12',                      N'Vitamins',      900.00, 1),
        (11, N'URINE',   N'Urine Routine',                    N'Urine',         150.00, 1),
        (12, N'ESR',     N'ESR',                              N'Haematology',   100.00, 1),
        (13, N'CRP',     N'C-Reactive Protein (CRP)',         N'Immunology',    400.00, 1),
        (14, N'CREAT',   N'Serum Creatinine',                 N'Biochemistry',  200.00, 1),
        (15, N'ELECT',   N'Serum Electrolytes',               N'Biochemistry',  500.00, 1);
    SET IDENTITY_INSERT dbo.TestMaster OFF;
END
GO

/* ------------------------------------------------------------ Quick check */
SELECT 'Client' AS TableName, COUNT(*) AS TotalRows FROM dbo.Client
UNION ALL SELECT 'TestMaster', COUNT(*) FROM dbo.TestMaster
UNION ALL SELECT 'Patient', COUNT(*) FROM dbo.Patient
UNION ALL SELECT 'WorkOrder', COUNT(*) FROM dbo.WorkOrder
UNION ALL SELECT 'WorkOrderTestDetail', COUNT(*) FROM dbo.WorkOrderTestDetail;
GO
