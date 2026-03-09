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
