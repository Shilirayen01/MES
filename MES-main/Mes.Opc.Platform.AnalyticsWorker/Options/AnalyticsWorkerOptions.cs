namespace Mes.Opc.Platform.AnalyticsWorker.Options;

public sealed class AnalyticsWorkerOptions
{
    /// <summary>How often the worker polls SQL for completed runs.</summary>
    public int PollIntervalSeconds { get; set; } = 10;

    /// <summary>Max runs processed per loop.</summary>
    public int BatchSize { get; set; } = 20;

    /// <summary>How far back we look for completed runs that still need processing.</summary>
    public int MaxLookbackDays { get; set; } = 14;

    /// <summary>Default lookback (minutes) used to find "as-of start" values.</summary>
    public int DefaultLookbackMinutes { get; set; } = 10;

    /// <summary>If a gap between samples is larger than this, TWA/PercentInRange can either cut or return NULL (config per variable).</summary>
    public int DefaultMaxGapSeconds { get; set; } = 600;

    /// <summary>SQL Server connection string name (from ConnectionStrings).</summary>
    public string ConnectionStringName { get; set; } = "MesOpcDb";
}
