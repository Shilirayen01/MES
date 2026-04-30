using Dapper;
using Mes.Opc.Platform.AnalyticsWorker.Domain;
using Microsoft.Extensions.Logging;

namespace Mes.Opc.Platform.AnalyticsWorker.Data;

public sealed class ProductionRunRepository
{
    private readonly SqlConnectionFactory _factory;
    private readonly ILogger<ProductionRunRepository> _log;

    public ProductionRunRepository(SqlConnectionFactory factory, ILogger<ProductionRunRepository> log)
    {
        _factory = factory;
        _log = log;
    }

    public async Task<IReadOnlyList<ProductionRunRow>> GetCompletedRunsNeedingProcessingAsync(
        int batchSize,
        int maxLookbackDays,
        CancellationToken ct)
    {
        const string sql = @"
                SELECT TOP (@BatchSize)
                    r.RunId,
                    r.MachineCode,
                    r.StartTs,
                    r.EndTs,
                    r.Status,
                    r.ScopeKey

                FROM ProductionRun r
                LEFT JOIN ProductionRunAnalyticsStatus s ON s.RunId = r.RunId
                WHERE r.Status = 'Completed'
                  AND r.EndTs IS NOT NULL
                  AND r.EndTs >= DATEADD(day, -@MaxLookbackDays, SYSUTCDATETIME())
                  AND (
                        s.RunId IS NULL
                        OR s.KpiComputedAt IS NULL
                        OR s.SummaryComputedAt IS NULL
                        OR s.QualityComputedAt IS NULL
                      )
                ORDER BY r.EndTs DESC;";

        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<ProductionRunRow>(new CommandDefinition(
            sql,
            new { BatchSize = batchSize, MaxLookbackDays = maxLookbackDays },
            cancellationToken: ct));

        return rows.AsList();
    }
}
