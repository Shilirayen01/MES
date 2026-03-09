using Dapper;

namespace Mes.Opc.Platform.AnalyticsWorker.Data;

public sealed class ResultRepository
{
    private readonly SqlConnectionFactory _factory;

    public ResultRepository(SqlConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<(DateTime? KpiComputedAt, DateTime? SummaryComputedAt, DateTime? QualityComputedAt)> GetRunStatusAsync(Guid runId, CancellationToken ct)
    {
        const string sql = @"
        SELECT KpiComputedAt, SummaryComputedAt, QualityComputedAt
        FROM ProductionRunAnalyticsStatus
        WHERE RunId = @RunId;";

        using var conn = _factory.Create();
        var row = await conn.QueryFirstOrDefaultAsync<StatusRow>(new CommandDefinition(sql, new { RunId = runId }, cancellationToken: ct));
        return row is null ? (null, null, null) : (row.KpiComputedAt, row.SummaryComputedAt, row.QualityComputedAt);
    }

    public async Task UpsertKpiResultAsync(Guid runId, string kpiCode, double? value, string? unit, string? detailsJson, CancellationToken ct)
    {
        const string sql = @"
MERGE ProductionRunKpiResult AS tgt
USING (SELECT @RunId AS RunId, @KpiCode AS KpiCode) AS src
ON tgt.RunId = src.RunId AND tgt.KpiCode = src.KpiCode
WHEN MATCHED THEN
  UPDATE SET Value = @Value, Unit = @Unit, DetailsJson = @DetailsJson, ComputedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
  INSERT (RunId, KpiCode, Value, Unit, DetailsJson, ComputedAt)
  VALUES (@RunId, @KpiCode, @Value, @Unit, @DetailsJson, SYSUTCDATETIME());";

        using var conn = _factory.Create();
        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            RunId = runId,
            KpiCode = kpiCode,
            Value = value,
            Unit = unit,
            DetailsJson = detailsJson
        }, cancellationToken: ct));
    }

    public async Task UpsertSummaryValueAsync(Guid runId, string fieldName, double? value, string? unit, string? detailsJson, CancellationToken ct)
    {
        const string sql = @"
MERGE ProductionRunSummaryValue AS tgt
USING (SELECT @RunId AS RunId, @FieldName AS FieldName) AS src
ON tgt.RunId = src.RunId AND tgt.FieldName = src.FieldName
WHEN MATCHED THEN
  UPDATE SET Value = @Value, Unit = @Unit, DetailsJson = @DetailsJson, ComputedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
  INSERT (RunId, FieldName, Value, Unit, DetailsJson, ComputedAt)
  VALUES (@RunId, @FieldName, @Value, @Unit, @DetailsJson, SYSUTCDATETIME());";

        using var conn = _factory.Create();
        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            RunId = runId,
            FieldName = fieldName,
            Value = value,
            Unit = unit,
            DetailsJson = detailsJson
        }, cancellationToken: ct));
    }

    public async Task UpsertQualityAsync(Guid runId, bool? isOk, string? reasonJson, CancellationToken ct)
    {
        const string sql = @"
MERGE ProductionRunQuality AS tgt
USING (SELECT @RunId AS RunId) AS src
ON tgt.RunId = src.RunId
WHEN MATCHED THEN
  UPDATE SET IsOk = @IsOk, ReasonJson = @ReasonJson, ComputedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
  INSERT (RunId, IsOk, ReasonJson, ComputedAt)
  VALUES (@RunId, @IsOk, @ReasonJson, SYSUTCDATETIME());";

        using var conn = _factory.Create();
        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            RunId = runId,
            IsOk = isOk,
            ReasonJson = reasonJson
        }, cancellationToken: ct));
    }

    public async Task<bool?> GetQualityIsOkAsync(Guid runId, CancellationToken ct)
    {
        const string sql = "SELECT IsOk FROM ProductionRunQuality WHERE RunId=@RunId;";
        using var conn = _factory.Create();
        return await conn.QueryFirstOrDefaultAsync<bool?>(new CommandDefinition(sql, new { RunId = runId }, cancellationToken: ct));
    }

    public async Task MarkModuleComputedAsync(Guid runId, string module, string? error, CancellationToken ct)
    {
        // module: "KPI" | "SUMMARY" | "QUALITY"
        var col = module.ToUpperInvariant() switch
        {
            "KPI" => "KpiComputedAt",
            "SUMMARY" => "SummaryComputedAt",
            "QUALITY" => "QualityComputedAt",
            _ => throw new ArgumentOutOfRangeException(nameof(module), module, "Unknown module.")
        };

        var sql = $@"
    MERGE ProductionRunAnalyticsStatus AS tgt
    USING (SELECT @RunId AS RunId) AS src
    ON tgt.RunId = src.RunId
    WHEN MATCHED THEN
      UPDATE SET {col} = SYSUTCDATETIME(), LastError = @Error, UpdatedAt = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
      INSERT (RunId, {col}, LastError, UpdatedAt)
      VALUES (@RunId, SYSUTCDATETIME(), @Error, SYSUTCDATETIME());";

        using var conn = _factory.Create();
        await conn.ExecuteAsync(new CommandDefinition(sql, new { RunId = runId, Error = error }, cancellationToken: ct));
    }

    public async Task MarkRunProcessedAsync(Guid runId, bool kpiDone, bool summaryDone, bool qualityDone, string? error, CancellationToken ct)
    {
        // Convenience: mark all flags at once (if a module isn't configured, caller can pass false).
        const string sql = @"
MERGE ProductionRunAnalyticsStatus AS tgt
USING (SELECT @RunId AS RunId) AS src
ON tgt.RunId = src.RunId
WHEN MATCHED THEN
  UPDATE SET
    KpiComputedAt = CASE WHEN @KpiDone = 1 THEN COALESCE(tgt.KpiComputedAt, SYSUTCDATETIME()) ELSE tgt.KpiComputedAt END,
    SummaryComputedAt = CASE WHEN @SummaryDone = 1 THEN COALESCE(tgt.SummaryComputedAt, SYSUTCDATETIME()) ELSE tgt.SummaryComputedAt END,
    QualityComputedAt = CASE WHEN @QualityDone = 1 THEN COALESCE(tgt.QualityComputedAt, SYSUTCDATETIME()) ELSE tgt.QualityComputedAt END,
    LastError = @Error,
    UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
  INSERT (RunId, KpiComputedAt, SummaryComputedAt, QualityComputedAt, LastError, UpdatedAt)
  VALUES (
    @RunId,
    CASE WHEN @KpiDone = 1 THEN SYSUTCDATETIME() END,
    CASE WHEN @SummaryDone = 1 THEN SYSUTCDATETIME() END,
    CASE WHEN @QualityDone = 1 THEN SYSUTCDATETIME() END,
    @Error,
    SYSUTCDATETIME()
  );";

        using var conn = _factory.Create();
        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            RunId = runId,
            KpiDone = kpiDone ? 1 : 0,
            SummaryDone = summaryDone ? 1 : 0,
            QualityDone = qualityDone ? 1 : 0,
            Error = error
        }, cancellationToken: ct));
    }

    private sealed record StatusRow(DateTime? KpiComputedAt, DateTime? SummaryComputedAt, DateTime? QualityComputedAt);
}
