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
