namespace Mes.Opc.Platform.AnalyticsWorker.Domain;

public sealed record ProductionRunRow(
    Guid RunId,
    string MachineCode,
    DateTime StartTs,
    DateTime EndTs,
    string Status,
   string ScopeKey);
