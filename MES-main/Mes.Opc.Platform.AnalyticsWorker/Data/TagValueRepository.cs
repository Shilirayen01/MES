using Dapper;
using Mes.Opc.Platform.AnalyticsWorker.Domain;

namespace Mes.Opc.Platform.AnalyticsWorker.Data;

public sealed class TagValueRepository
{
    private readonly SqlConnectionFactory _factory;

    public TagValueRepository(SqlConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<TagPoint>> GetDedupedPointsAsync(
    ProductionRunRow run,
    string tagNodeId,
    TagScope scope,
    int lookbackMinutes,
    CancellationToken ct)
    {
        // We always include a small lookback to be able to compute "as-of start".
        var startMinus = run.StartTs.AddMinutes(-lookbackMinutes);

        // ✅ PATCH MULTI-SPOOL : résoudre {ScopeKey} -> SP1/SP2/SP3...
        var resolvedTagNodeId = tagNodeId;
        if (!string.IsNullOrWhiteSpace(resolvedTagNodeId)
            && resolvedTagNodeId.Contains("{ScopeKey}")
            && !string.IsNullOrWhiteSpace(run.ScopeKey))
        {
            resolvedTagNodeId = resolvedTagNodeId.Replace("{ScopeKey}", run.ScopeKey);
        }

        string sql = scope switch
        {
            TagScope.Product => @"
            WITH raw AS (
                SELECT
                    SourceTimestamp,
                    TRY_CONVERT(float, Value) AS V,
                    CreatedDate,
                    Id,
                    ROW_NUMBER() OVER (PARTITION BY SourceTimestamp ORDER BY CreatedDate DESC, Id DESC) AS rn
                FROM MachineTagValue
                WHERE ProductionRunId = @RunId
                  AND OpcNodeId = @TagNodeId
                  AND SourceTimestamp <= @EndTs
            )
            SELECT SourceTimestamp AS [Timestamp], CAST(V AS float) AS [Value]
            FROM raw
            WHERE rn = 1 AND V IS NOT NULL
            ORDER BY SourceTimestamp;",
                        _ => @"
            WITH raw AS (
                SELECT
                    SourceTimestamp,
                    TRY_CONVERT(float, Value) AS V,
                    CreatedDate,
                    Id,
                    ROW_NUMBER() OVER (PARTITION BY SourceTimestamp ORDER BY CreatedDate DESC, Id DESC) AS rn
                FROM MachineTagValue
                WHERE MachineCode = @MachineCode
                  AND OpcNodeId = @TagNodeId
                  AND ProductionRunId IS NULL
                  AND SourceTimestamp <= @EndTs
                  AND SourceTimestamp >= @StartMinus
            )
            SELECT SourceTimestamp AS [Timestamp], CAST(V AS float) AS [Value]
            FROM raw
            WHERE rn = 1 AND V IS NOT NULL
            ORDER BY SourceTimestamp;"
        };

        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<TagPoint>(new CommandDefinition(
            sql,
            new
            {
                run.RunId,
                run.MachineCode,
                TagNodeId = resolvedTagNodeId,   // ✅ ICI: on utilise le TagNodeId résolu
                StartMinus = startMinus,
                run.EndTs
            },
            cancellationToken: ct));

        return rows.AsList();
    }

}
