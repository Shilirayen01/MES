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
