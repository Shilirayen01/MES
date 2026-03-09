using Mes.Opc.Platform.Data.Db;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mes.Opc.Platform.Api.Controllers;

/// <summary>
/// Read-only endpoint for querying analytics results (KPIs + Summary values)
/// computed by the AnalyticsWorker after each production run.
/// Route: api/v1/analytics/results
/// </summary>
[ApiController]
[Route("api/v1/analytics/results")]
public sealed class AnalyticsResultsController : ControllerBase
{
    private readonly OpcDbContext _db;

    public AnalyticsResultsController(OpcDbContext db) => _db = db;

    /// <summary>
    /// Returns production runs with their computed KPI and Summary values.
    /// </summary>
    [HttpGet("{machineCode}")]
    public async Task<IActionResult> GetResults(
        string machineCode,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-7);
        var toDate = to ?? DateTime.UtcNow;

        var conn = _db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);

        // 1. Production runs
        var runs = new List<RunDto>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
SELECT TOP (@Limit) RunId, MachineCode, ScopeKey, StartTs, EndTs, Status, EndReason
FROM dbo.ProductionRun
WHERE MachineCode = @MachineCode AND StartTs >= @From AND StartTs <= @To
ORDER BY StartTs DESC;";
            AddParam(cmd, "@Limit", limit);
            AddParam(cmd, "@MachineCode", machineCode);
            AddParam(cmd, "@From", fromDate);
            AddParam(cmd, "@To", toDate);

            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                runs.Add(new RunDto
                {
                    RunId = reader.GetGuid(0),
                    MachineCode = reader.GetString(1),
                    ScopeKey = reader.GetString(2),
                    StartTs = reader.GetDateTime(3),
                    EndTs = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                    Status = reader.GetString(5),
                    EndReason = reader.IsDBNull(6) ? null : reader.GetString(6)
                });
            }
        }

        if (runs.Count == 0)
            return Ok(Array.Empty<object>());

        var runIds = runs.Select(r => r.RunId).ToHashSet();

        // 2. Summary values
        var summaries = new Dictionary<Guid, List<SummaryValueDto>>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
SELECT RunId, FieldName, Value, Unit, ComputedAt
FROM dbo.ProductionRunSummaryValue
ORDER BY FieldName;";

            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var runId = reader.GetGuid(0);
                if (!runIds.Contains(runId)) continue;

                if (!summaries.ContainsKey(runId))
                    summaries[runId] = new List<SummaryValueDto>();

                summaries[runId].Add(new SummaryValueDto
                {
                    FieldName = reader.GetString(1),
                    Value = reader.IsDBNull(2) ? null : reader.GetDouble(2),
                    Unit = reader.IsDBNull(3) ? null : reader.GetString(3),
                    ComputedAt = reader.IsDBNull(4) ? null : reader.GetDateTime(4)
                });
            }
        }

        // 3. KPI results
        var kpis = new Dictionary<Guid, List<KpiValueDto>>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
SELECT RunId, KpiCode, Value, Unit, ComputedAt
FROM dbo.ProductionRunKpiResult
ORDER BY KpiCode;";

            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var runId = reader.GetGuid(0);
                if (!runIds.Contains(runId)) continue;

                if (!kpis.ContainsKey(runId))
                    kpis[runId] = new List<KpiValueDto>();

                kpis[runId].Add(new KpiValueDto
                {
                    KpiCode = reader.GetString(1),
                    Value = reader.IsDBNull(2) ? null : reader.GetDouble(2),
                    Unit = reader.IsDBNull(3) ? null : reader.GetString(3),
                    ComputedAt = reader.IsDBNull(4) ? null : reader.GetDateTime(4)
                });
            }
        }

        // 4. Build response
        var result = runs.Select(r => new
        {
            r.RunId,
            r.MachineCode,
            r.ScopeKey,
            r.StartTs,
            r.EndTs,
            r.Status,
            r.EndReason,
            DurationSeconds = r.EndTs.HasValue ? (int)(r.EndTs.Value - r.StartTs).TotalSeconds : (int?)null,
            SummaryValues = summaries.TryGetValue(r.RunId, out var sv) ? sv : new List<SummaryValueDto>(),
            KpiValues = kpis.TryGetValue(r.RunId, out var kv) ? kv : new List<KpiValueDto>()
        });

        return Ok(result);
    }

    private static void AddParam(System.Data.Common.DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }

    // ── DTOs ─────────────────────────────────────────────────────────────────

    private sealed class RunDto
    {
        public Guid RunId { get; set; }
        public string MachineCode { get; set; } = "";
        public string ScopeKey { get; set; } = "";
        public DateTime StartTs { get; set; }
        public DateTime? EndTs { get; set; }
        public string Status { get; set; } = "";
        public string? EndReason { get; set; }
    }

    private sealed class SummaryValueDto
    {
        public string FieldName { get; set; } = "";
        public double? Value { get; set; }
        public string? Unit { get; set; }
        public DateTime? ComputedAt { get; set; }
    }

    private sealed class KpiValueDto
    {
        public string KpiCode { get; set; } = "";
        public double? Value { get; set; }
        public string? Unit { get; set; }
        public DateTime? ComputedAt { get; set; }
    }
}
