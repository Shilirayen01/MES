-- -----------------------------------------------------------------------------
-- RunAll.sql (concaténé)
-- Exécute tous les scripts SQL dans l'ordre.
-- IMPORTANT: exécute dans la bonne base (USE [YourDb]).
-- -----------------------------------------------------------------------------

SET NOCOUNT ON;

PRINT 'Running 001_CreateProductionRun.sql';
GO
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

GO

PRINT 'Running 002_CreateMachineCycleRule.sql';
GO
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

GO

PRINT 'Running 003_TvpAndProc_V2.sql';
GO
-- -------------------------------------------------------------------------------------------------
-- 003_TvpAndProc_V2.sql
--
-- Upgrades the TVP + stored procedure used for batched inserts of MachineTagValue.
--
-- IMPORTANT (RealTime OPC / NSU):
-- NodeId strings stored and transmitted by the runtime may now contain the stable
-- "nsu=<NamespaceUri>;..." form, which is longer than legacy "ns=<index>;...".
-- To avoid truncation, we use NVARCHAR(1200) for OpcNodeId everywhere.
-- -------------------------------------------------------------------------------------------------

/*
Expected objects:
- dbo.MachineTagValue_TVP_V2   (TVP with ProductionRunId + OpcNodeId NVARCHAR(1200))
- dbo.InsertMachineTagValues_Batch_V2 (proc consuming TVP_V2)

This script is idempotent and upgrades an existing TVP if it was created
with a smaller OpcNodeId length (e.g., NVARCHAR(256)).
*/

DECLARE @needsUpgrade BIT = 0;

IF TYPE_ID('dbo.MachineTagValue_TVP_V2') IS NOT NULL
BEGIN
    DECLARE @maxLenBytes INT;

    SELECT @maxLenBytes = c.max_length
    FROM sys.table_types tt
    JOIN sys.columns c ON c.object_id = tt.type_table_object_id
    WHERE tt.name = 'MachineTagValue_TVP_V2'
      AND SCHEMA_NAME(tt.schema_id) = 'dbo'
      AND c.name = 'OpcNodeId';

    -- NVARCHAR length is stored in bytes (2 bytes/char). NVARCHAR(1200) => 2400.
    IF (@maxLenBytes IS NOT NULL AND @maxLenBytes < 2400)
        SET @needsUpgrade = 1;
END
ELSE
BEGIN
    SET @needsUpgrade = 1;
END

IF (@needsUpgrade = 1)
BEGIN
    -- Drop proc first if it exists (it depends on the TVP type).
    IF OBJECT_ID('dbo.InsertMachineTagValues_Batch_V2','P') IS NOT NULL
        DROP PROCEDURE dbo.InsertMachineTagValues_Batch_V2;

    -- Drop and recreate TVP with the correct length.
    IF TYPE_ID('dbo.MachineTagValue_TVP_V2') IS NOT NULL
        DROP TYPE dbo.MachineTagValue_TVP_V2;

    CREATE TYPE dbo.MachineTagValue_TVP_V2 AS TABLE(
        ProductionRunId UNIQUEIDENTIFIER NULL,
        MachineCode NVARCHAR(50) NOT NULL,
        OpcNodeId NVARCHAR(1200) NOT NULL,
        Value NVARCHAR(200) NULL,
        StatusCode NVARCHAR(200) NOT NULL,
        SourceTimestamp DATETIME2(3) NULL
    );

    EXEC('CREATE PROCEDURE dbo.InsertMachineTagValues_Batch_V2 @Values dbo.MachineTagValue_TVP_V2 READONLY AS BEGIN SET NOCOUNT ON; END');
END
GO

ALTER PROCEDURE dbo.InsertMachineTagValues_Batch_V2
    @Values dbo.MachineTagValue_TVP_V2 READONLY
AS
BEGIN
    SET NOCOUNT ON;

    -- assumes table MachineTagValue has a nullable ProductionRunId column (optional)
    INSERT INTO dbo.MachineTagValue(ProductionRunId, MachineCode, OpcNodeId, Value, StatusCode, SourceTimestamp)
    SELECT ProductionRunId, MachineCode, OpcNodeId, Value, StatusCode, SourceTimestamp
    FROM @Values;
END
GO

GO

PRINT 'Running 004_AddRecoveryColumns_MachineCycleRule.sql';
GO
USE [OpcDb];
GO

-- ------------------------------------------------------------
-- 004_AddRecoveryColumns_MachineCycleRule.sql
--
-- Adds optional recovery configuration columns to dbo.MachineCycleRule.
--
-- The runtime will automatically fall back to the old schema if these
-- columns are not present, but to use Recovery you should apply this.
-- ------------------------------------------------------------

IF COL_LENGTH('dbo.MachineCycleRule', 'RecoveryStrategy') IS NULL
BEGIN
    ALTER TABLE dbo.MachineCycleRule
    ADD RecoveryStrategy NVARCHAR(30) NULL;
END
GO

IF COL_LENGTH('dbo.MachineCycleRule', 'RecoveryConfirmNodeId') IS NULL
BEGIN
    ALTER TABLE dbo.MachineCycleRule
    ADD RecoveryConfirmNodeId NVARCHAR(256) NULL;
END
GO

IF COL_LENGTH('dbo.MachineCycleRule', 'RecoveryConfirmDelta') IS NULL
BEGIN
    ALTER TABLE dbo.MachineCycleRule
    ADD RecoveryConfirmDelta DECIMAL(18,3) NULL;
END
GO

IF COL_LENGTH('dbo.MachineCycleRule', 'RecoveryConfirmWindowSeconds') IS NULL
BEGIN
    ALTER TABLE dbo.MachineCycleRule
    ADD RecoveryConfirmWindowSeconds INT NULL;
END
GO

-- Recommended defaults (optional)
UPDATE dbo.MachineCycleRule
SET RecoveryConfirmWindowSeconds = COALESCE(RecoveryConfirmWindowSeconds, 5)
WHERE RecoveryStrategy IS NOT NULL;
GO

GO

PRINT 'Running 005_WidenNodeIdColumns_For_NSU.sql';
GO
-- -------------------------------------------------------------------------------------------------
-- 005_WidenNodeIdColumns_For_NSU.sql
--
-- Ensures database columns that store NodeId strings are large enough for the
-- stable "nsu=<NamespaceUri>;..." format (NVARCHAR(1200)).
--
-- This script is safe to run multiple times (idempotent).
-- -------------------------------------------------------------------------------------------------

DECLARE @sql NVARCHAR(MAX);

-- Helper: widen a column to NVARCHAR(1200) while preserving NULL/NOT NULL.
-- (We use dynamic SQL because NULLability must be preserved.)
IF OBJECT_ID('dbo.MachineTagValue','U') IS NOT NULL AND COL_LENGTH('dbo.MachineTagValue','OpcNodeId') IS NOT NULL
BEGIN
    DECLARE @len INT = (
        SELECT CHARACTER_MAXIMUM_LENGTH
        FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME='MachineTagValue' AND COLUMN_NAME='OpcNodeId'
    );

    IF (@len IS NOT NULL AND @len < 1200)
    BEGIN
        DECLARE @nullable BIT = (
            SELECT is_nullable
            FROM sys.columns
            WHERE object_id = OBJECT_ID('dbo.MachineTagValue') AND name = 'OpcNodeId'
        );

        SET @sql = N'ALTER TABLE dbo.MachineTagValue ALTER COLUMN OpcNodeId NVARCHAR(1200) ' +
                   CASE WHEN @nullable = 1 THEN N'NULL' ELSE N'NOT NULL' END + N';';
        EXEC(@sql);
    END
END
GO

IF OBJECT_ID('dbo.MachineTagMapping','U') IS NOT NULL AND COL_LENGTH('dbo.MachineTagMapping','OpcNodeId') IS NOT NULL
BEGIN
    DECLARE @len2 INT = (
        SELECT CHARACTER_MAXIMUM_LENGTH
        FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME='MachineTagMapping' AND COLUMN_NAME='OpcNodeId'
    );

    IF (@len2 IS NOT NULL AND @len2 < 1200)
    BEGIN
        DECLARE @nullable2 BIT = (
            SELECT is_nullable
            FROM sys.columns
            WHERE object_id = OBJECT_ID('dbo.MachineTagMapping') AND name = 'OpcNodeId'
        );

        SET @sql = N'ALTER TABLE dbo.MachineTagMapping ALTER COLUMN OpcNodeId NVARCHAR(1200) ' +
                   CASE WHEN @nullable2 = 1 THEN N'NULL' ELSE N'NOT NULL' END + N';';
        EXEC(@sql);
    END
END
GO

IF OBJECT_ID('dbo.MachineCycleRule','U') IS NOT NULL
BEGIN
    DECLARE @cols TABLE (ColName SYSNAME);
    INSERT INTO @cols(ColName)
    VALUES
        ('StartNodeId'),
        ('EndPrimaryNodeId'),
        ('EndFallbackNodeId'),
        ('ValidationNodeId_Speed'),
        ('ValidationNodeId_State'),
        ('RecoveryConfirmNodeId');

    DECLARE @c SYSNAME;

    DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
        SELECT ColName FROM @cols WHERE COL_LENGTH('dbo.MachineCycleRule', ColName) IS NOT NULL;

    OPEN cur;
    FETCH NEXT FROM cur INTO @c;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        DECLARE @l INT = (
            SELECT CHARACTER_MAXIMUM_LENGTH
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME='MachineCycleRule' AND COLUMN_NAME=@c
        );

        IF (@l IS NOT NULL AND @l < 1200)
        BEGIN
            DECLARE @n BIT = (
                SELECT is_nullable
                FROM sys.columns
                WHERE object_id = OBJECT_ID('dbo.MachineCycleRule') AND name = @c
            );

            SET @sql = N'ALTER TABLE dbo.MachineCycleRule ALTER COLUMN ' + QUOTENAME(@c) +
                       N' NVARCHAR(1200) ' + CASE WHEN @n = 1 THEN N'NULL' ELSE N'NOT NULL' END + N';';
            EXEC(@sql);
        END

        FETCH NEXT FROM cur INTO @c;
    END

    CLOSE cur;
    DEALLOCATE cur;
END
GO

GO

PRINT 'Running 006_AddAndMigrate_NSU.sql';
GO
-- -------------------------------------------------------------------------------------------------
-- 006_AddAndMigrate_NSU.sql
--
-- Adds NSU-based NodeId storage and migrates dbo.MachineTagMapping.OpcNodeId -> OpcNodeIdNsu.
--
-- SAFE to re-run.
--
-- Convention used in this project:
-- - Namespace is the prefix before the first '.' of the string identifier after 's='.
--   Example: s=TFE_04_MMH.DB181_DATA_TO_SCADA.GENERAL.LineSpeed
--   => nsu=TFE_04_MMH;s=TFE_04_MMH.DB181_DATA_TO_SCADA.GENERAL.LineSpeed
-- -------------------------------------------------------------------------------------------------

SET NOCOUNT ON;

IF OBJECT_ID('dbo.MachineTagMapping', 'U') IS NULL
BEGIN
    PRINT 'dbo.MachineTagMapping not found. Skipping 006_AddAndMigrate_NSU.';
    RETURN;
END

IF COL_LENGTH('dbo.MachineTagMapping', 'OpcNodeIdNsu') IS NULL
BEGIN
    ALTER TABLE dbo.MachineTagMapping ADD OpcNodeIdNsu NVARCHAR(1200) NULL;
END

;WITH src AS
(
    SELECT
        Id,
        OpcNodeId = LTRIM(RTRIM(OpcNodeId))
    FROM dbo.MachineTagMapping
    WHERE (OpcNodeIdNsu IS NULL OR LTRIM(RTRIM(OpcNodeIdNsu)) = N'')
      AND OpcNodeId IS NOT NULL
      AND LTRIM(RTRIM(OpcNodeId)) <> N''
), parts AS
(
    SELECT
        Id,
        OpcNodeId,
        SPos = CHARINDEX(N's=', OpcNodeId)
    FROM src
), svalue AS
(
    SELECT
        Id,
        OpcNodeId,
        SPos,
        SPart = CASE WHEN SPos > 0 THEN SUBSTRING(OpcNodeId, SPos, 1200) END,
        SId   = CASE WHEN SPos > 0 THEN SUBSTRING(OpcNodeId, SPos + 2, 1200) END
    FROM parts
), prefix AS
(
    SELECT
        Id,
        OpcNodeId,
        SPart,
        Prefix = CASE
            WHEN SId IS NOT NULL AND CHARINDEX(N'.', SId) > 1 THEN LEFT(SId, CHARINDEX(N'.', SId) - 1)
            ELSE NULL
        END
    FROM svalue
)
UPDATE m
SET OpcNodeIdNsu = CASE
    WHEN p.OpcNodeId LIKE N'nsu=%;%' THEN p.OpcNodeId
    WHEN p.Prefix IS NOT NULL AND p.SPart IS NOT NULL THEN N'nsu=' + p.Prefix + N';' + p.SPart
    ELSE NULL
END
FROM dbo.MachineTagMapping m
JOIN prefix p ON p.Id = m.Id;

-- Show any rows we could not migrate automatically
IF EXISTS (
    SELECT 1
    FROM dbo.MachineTagMapping
    WHERE (OpcNodeIdNsu IS NULL OR LTRIM(RTRIM(OpcNodeIdNsu)) = N'')
      AND OpcNodeId IS NOT NULL
      AND LTRIM(RTRIM(OpcNodeId)) <> N''
)
BEGIN
    PRINT 'WARNING: some MachineTagMapping rows could not be migrated to NSU automatically.';
    SELECT TOP (200)
        Id, MachineCode, OpcNodeId, OpcNodeIdNsu
    FROM dbo.MachineTagMapping
    WHERE (OpcNodeIdNsu IS NULL OR LTRIM(RTRIM(OpcNodeIdNsu)) = N'')
      AND OpcNodeId IS NOT NULL
      AND LTRIM(RTRIM(OpcNodeId)) <> N''
    ORDER BY Id;
END

GO

PRINT 'Running 007_AddAndMigrate_NSU_MachineCycleRule.sql';
GO
-- -------------------------------------------------------------------------------------------------
-- 007_AddAndMigrate_NSU_MachineCycleRule.sql
--
-- Adds NSU columns to dbo.MachineCycleRule and migrates legacy NodeId columns.
--
-- SAFE to re-run.
--
-- Convention used in this project:
-- - Namespace is derived from the prefix before the first '.' of the identifier after 's='.
--   Example: ns=23;s=TFE_06_MMH.DB311_DATA_TO_SCADA.SP1_LengthCounter_ACT
--   => nsu=TFE_06_MMH;s=TFE_06_MMH.DB311_DATA_TO_SCADA.SP1_LengthCounter_ACT
-- -------------------------------------------------------------------------------------------------

SET NOCOUNT ON;

IF OBJECT_ID('dbo.MachineCycleRule', 'U') IS NULL
BEGIN
    PRINT 'dbo.MachineCycleRule not found. Skipping 007_AddAndMigrate_NSU_MachineCycleRule.';
    RETURN;
END

-- Helper function to convert various string NodeId inputs to the project's NSU convention.
-- Returns NULL when conversion is not possible (unexpected format).
CREATE OR ALTER FUNCTION dbo.fn_ToProjectNsuNodeId (@nodeId NVARCHAR(1200))
RETURNS NVARCHAR(1200)
AS
BEGIN
    DECLARE @n NVARCHAR(1200) = LTRIM(RTRIM(@nodeId));
    IF @n IS NULL OR @n = N'' RETURN NULL;

    -- Already NSU
    IF @n LIKE N'nsu=%;%' RETURN @n;

    -- Locate 's=' (StringNodeId)
    DECLARE @spos INT = CHARINDEX(N's=', @n);
    IF @spos = 0 RETURN NULL;

    DECLARE @spart NVARCHAR(1200) = SUBSTRING(@n, @spos, 1200);      -- starts with 's='
    DECLARE @sid   NVARCHAR(1200) = SUBSTRING(@n, @spos + 2, 1200);  -- identifier after 's='

    DECLARE @dot INT = CHARINDEX(N'.', @sid);
    IF @dot <= 1 RETURN NULL;

    DECLARE @prefix NVARCHAR(200) = LEFT(@sid, @dot - 1);

    RETURN N'nsu=' + @prefix + N';' + @spart;
END
GO

-- 1) Add NSU columns (nullable)
IF COL_LENGTH('dbo.MachineCycleRule', 'StartNodeIdNsu') IS NULL
    ALTER TABLE dbo.MachineCycleRule ADD StartNodeIdNsu NVARCHAR(1200) NULL;
IF COL_LENGTH('dbo.MachineCycleRule', 'EndPrimaryNodeIdNsu') IS NULL
    ALTER TABLE dbo.MachineCycleRule ADD EndPrimaryNodeIdNsu NVARCHAR(1200) NULL;
IF COL_LENGTH('dbo.MachineCycleRule', 'EndFallbackNodeIdNsu') IS NULL
    ALTER TABLE dbo.MachineCycleRule ADD EndFallbackNodeIdNsu NVARCHAR(1200) NULL;
IF COL_LENGTH('dbo.MachineCycleRule', 'ValidationNodeId_SpeedNsu') IS NULL
    ALTER TABLE dbo.MachineCycleRule ADD ValidationNodeId_SpeedNsu NVARCHAR(1200) NULL;
IF COL_LENGTH('dbo.MachineCycleRule', 'ValidationNodeId_StateNsu') IS NULL
    ALTER TABLE dbo.MachineCycleRule ADD ValidationNodeId_StateNsu NVARCHAR(1200) NULL;
IF COL_LENGTH('dbo.MachineCycleRule', 'RecoveryConfirmNodeIdNsu') IS NULL
    ALTER TABLE dbo.MachineCycleRule ADD RecoveryConfirmNodeIdNsu NVARCHAR(1200) NULL;
IF COL_LENGTH('dbo.MachineCycleRule', 'AbortNodeIdsNsu') IS NULL
    ALTER TABLE dbo.MachineCycleRule ADD AbortNodeIdsNsu NVARCHAR(1200) NULL;
GO

-- 2) Populate NSU columns where empty
UPDATE r
SET
    StartNodeIdNsu = CASE WHEN (r.StartNodeIdNsu IS NULL OR LTRIM(RTRIM(r.StartNodeIdNsu)) = N'') THEN dbo.fn_ToProjectNsuNodeId(r.StartNodeId) ELSE r.StartNodeIdNsu END,
    EndPrimaryNodeIdNsu = CASE WHEN (r.EndPrimaryNodeIdNsu IS NULL OR LTRIM(RTRIM(r.EndPrimaryNodeIdNsu)) = N'') THEN dbo.fn_ToProjectNsuNodeId(r.EndPrimaryNodeId) ELSE r.EndPrimaryNodeIdNsu END,
    EndFallbackNodeIdNsu = CASE WHEN (r.EndFallbackNodeIdNsu IS NULL OR LTRIM(RTRIM(r.EndFallbackNodeIdNsu)) = N'') THEN dbo.fn_ToProjectNsuNodeId(r.EndFallbackNodeId) ELSE r.EndFallbackNodeIdNsu END,
    ValidationNodeId_SpeedNsu = CASE WHEN (r.ValidationNodeId_SpeedNsu IS NULL OR LTRIM(RTRIM(r.ValidationNodeId_SpeedNsu)) = N'') THEN dbo.fn_ToProjectNsuNodeId(r.ValidationNodeId_Speed) ELSE r.ValidationNodeId_SpeedNsu END,
    ValidationNodeId_StateNsu = CASE WHEN (r.ValidationNodeId_StateNsu IS NULL OR LTRIM(RTRIM(r.ValidationNodeId_StateNsu)) = N'') THEN dbo.fn_ToProjectNsuNodeId(r.ValidationNodeId_State) ELSE r.ValidationNodeId_StateNsu END,
    RecoveryConfirmNodeIdNsu = CASE WHEN (r.RecoveryConfirmNodeIdNsu IS NULL OR LTRIM(RTRIM(r.RecoveryConfirmNodeIdNsu)) = N'') THEN dbo.fn_ToProjectNsuNodeId(r.RecoveryConfirmNodeId) ELSE r.RecoveryConfirmNodeIdNsu END
FROM dbo.MachineCycleRule r;
GO

-- 3) AbortNodeIdsNsu: best-effort migration for ';' separated lists
BEGIN TRY
    ;WITH split AS
    (
        SELECT
            r.MachineCode,
            r.ScopeKey,
            value = LTRIM(RTRIM(s.value))
        FROM dbo.MachineCycleRule r
        CROSS APPLY STRING_SPLIT(ISNULL(r.AbortNodeIds, N''), N';') s
        WHERE ISNULL(LTRIM(RTRIM(s.value)), N'') <> N''
    ), conv AS
    (
        SELECT
            MachineCode,
            ScopeKey,
            AbortItemNsu = dbo.fn_ToProjectNsuNodeId(value)
        FROM split
        WHERE dbo.fn_ToProjectNsuNodeId(value) IS NOT NULL
    ), agg AS
    (
        SELECT
            MachineCode,
            ScopeKey,
            AbortNodeIdsNsu = STRING_AGG(AbortItemNsu, N';')
        FROM conv
        GROUP BY MachineCode, ScopeKey
    )
    UPDATE r
    SET r.AbortNodeIdsNsu = a.AbortNodeIdsNsu
    FROM dbo.MachineCycleRule r
    JOIN agg a ON a.MachineCode = r.MachineCode AND a.ScopeKey = r.ScopeKey
    WHERE (r.AbortNodeIdsNsu IS NULL OR LTRIM(RTRIM(r.AbortNodeIdsNsu)) = N'');
END TRY
BEGIN CATCH
    PRINT 'WARNING: could not migrate AbortNodeIdsNsu (STRING_AGG/STRING_SPLIT not available?). Skipping.';
END CATCH;
GO

-- 4) Report remaining unmigrated NodeIds (optional)
IF EXISTS (
    SELECT 1
    FROM dbo.MachineCycleRule
    WHERE (StartNodeId IS NOT NULL AND (StartNodeIdNsu IS NULL OR LTRIM(RTRIM(StartNodeIdNsu)) = N''))
       OR (EndPrimaryNodeId IS NOT NULL AND (EndPrimaryNodeIdNsu IS NULL OR LTRIM(RTRIM(EndPrimaryNodeIdNsu)) = N''))
)
BEGIN
    PRINT 'WARNING: some MachineCycleRule node IDs could not be migrated to NSU automatically.';
    SELECT TOP (200)
        MachineCode,
        ScopeKey,
        StartNodeId, StartNodeIdNsu,
        EndPrimaryNodeId, EndPrimaryNodeIdNsu,
        ValidationNodeId_Speed, ValidationNodeId_SpeedNsu,
        ValidationNodeId_State, ValidationNodeId_StateNsu,
        RecoveryConfirmNodeId, RecoveryConfirmNodeIdNsu,
        AbortNodeIds, AbortNodeIdsNsu
    FROM dbo.MachineCycleRule
    WHERE (StartNodeId IS NOT NULL AND (StartNodeIdNsu IS NULL OR LTRIM(RTRIM(StartNodeIdNsu)) = N''))
       OR (EndPrimaryNodeId IS NOT NULL AND (EndPrimaryNodeIdNsu IS NULL OR LTRIM(RTRIM(EndPrimaryNodeIdNsu)) = N''))
    ORDER BY MachineCode, ScopeKey;
END
GO

GO

