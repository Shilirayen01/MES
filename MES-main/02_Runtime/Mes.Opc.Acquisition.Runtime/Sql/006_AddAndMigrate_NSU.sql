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
