USE [OpcDb];
GO

/* 1) Empêcher les doublons de mapping par machine + node */
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'UX_MachineTagMapping_MachineCode_OpcNodeId'
      AND object_id = OBJECT_ID('dbo.MachineTagMapping')
)
BEGIN
    CREATE UNIQUE INDEX UX_MachineTagMapping_MachineCode_OpcNodeId
    ON dbo.MachineTagMapping (MachineCode, OpcNodeId);
END
GO

/* 2) Accélérer les requêtes d'historique par machine+node+temps */
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_MachineTagValue_MachineCode_OpcNodeId_SourceTimestamp'
      AND object_id = OBJECT_ID('dbo.MachineTagValue')
)
BEGIN
    CREATE INDEX IX_MachineTagValue_MachineCode_OpcNodeId_SourceTimestamp
    ON dbo.MachineTagValue (MachineCode, OpcNodeId, SourceTimestamp DESC)
    INCLUDE (StatusCode, CreatedDate);
END
GO
