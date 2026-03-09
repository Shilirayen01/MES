IF OBJECT_ID('dbo.MachineCycleRule','U') IS NOT NULL
    RETURN;

CREATE TABLE dbo.MachineCycleRule (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    MachineCode NVARCHAR(50) NOT NULL,
    ScopeKey NVARCHAR(50) NOT NULL,
    IsActive BIT NOT NULL DEFAULT(1),
    StartStrategy NVARCHAR(30) NOT NULL,
    StartNodeId NVARCHAR(256) NULL,
    StartEdgeType NVARCHAR(10) NULL,
    StartValue NVARCHAR(100) NULL,
    EndPrimaryStrategy NVARCHAR(30) NOT NULL,
    EndPrimaryNodeId NVARCHAR(256) NULL,
    EndPrimaryEdgeType NVARCHAR(10) NULL,
    EndFallbackStrategy NVARCHAR(30) NULL,
    EndFallbackNodeId NVARCHAR(256) NULL,
    AbortNodeIds NVARCHAR(MAX) NULL,
    DebounceMs INT NOT NULL DEFAULT(500),
    MinCycleSeconds INT NOT NULL DEFAULT(1),
    TimeoutSeconds INT NOT NULL DEFAULT(3600),
    Epsilon DECIMAL(18,3) NULL,
    TargetTolerance DECIMAL(18,3) NULL,
    ValidationNodeId_Speed NVARCHAR(256) NULL,
    ValidationSpeedMin DECIMAL(18,3) NULL,
    ValidationNodeId_State NVARCHAR(256) NULL,
    ValidationStateValue NVARCHAR(100) NULL,
    -- Recovery (optional): allows creating a Run when service starts mid-production
    RecoveryStrategy NVARCHAR(30) NULL,
    RecoveryConfirmNodeId NVARCHAR(256) NULL,
    RecoveryConfirmDelta DECIMAL(18,3) NULL,
    RecoveryConfirmWindowSeconds INT NULL
);

CREATE UNIQUE INDEX UX_MachineCycleRule_Machine_Scope ON dbo.MachineCycleRule(MachineCode, ScopeKey);
