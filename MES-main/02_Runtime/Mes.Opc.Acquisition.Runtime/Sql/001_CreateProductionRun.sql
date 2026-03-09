IF OBJECT_ID('dbo.ProductionRun','U') IS NOT NULL
    RETURN;

CREATE TABLE dbo.ProductionRun (
    RunId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    MachineCode NVARCHAR(50) NOT NULL,
    ScopeKey NVARCHAR(50) NOT NULL,
    StartTs DATETIME2(3) NOT NULL,
    EndTs DATETIME2(3) NULL,
    Status NVARCHAR(20) NOT NULL,
    EndReason NVARCHAR(100) NULL,
    MetaJson NVARCHAR(MAX) NULL
);

CREATE INDEX IX_ProductionRun_Machine_StartTs ON dbo.ProductionRun(MachineCode, StartTs);
