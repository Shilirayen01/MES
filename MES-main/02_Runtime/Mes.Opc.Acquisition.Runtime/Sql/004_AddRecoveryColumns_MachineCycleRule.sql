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
