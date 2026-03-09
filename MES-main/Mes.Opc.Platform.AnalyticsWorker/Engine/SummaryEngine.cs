using System.Text.Json;
using Mes.Opc.Platform.AnalyticsWorker.Data;
using Mes.Opc.Platform.AnalyticsWorker.Domain;
using Microsoft.Extensions.Logging;

namespace Mes.Opc.Platform.AnalyticsWorker.Engine;

public sealed class SummaryEngine
{
    private readonly TagAggregationEngine _agg;
    private readonly ResultRepository _results;
    private readonly ILogger<SummaryEngine> _log;

    public SummaryEngine(TagAggregationEngine agg, ResultRepository results, ILogger<SummaryEngine> log)
    {
        _agg = agg;
        _results = results;
        _log = log;
    }

    public async Task ComputeAndStoreIfNeededAsync(ProductionRunRow run, MachineConfig cfg, CancellationToken ct)
    {
        var status = await _results.GetRunStatusAsync(run.RunId, ct);
        if (status.SummaryComputedAt is not null)
        {
            _log.LogDebug("Skip Summary for RunId={RunId} (already computed at {At})", run.RunId, status.SummaryComputedAt);
            return;
        }

        var summary = cfg.Summary;
        if (summary is null || !summary.IsActive || summary.Items.Count == 0)
        {
            await _results.MarkModuleComputedAsync(run.RunId, module: "SUMMARY", error: null, ct);
            return;
        }

        var cache = new Dictionary<string, IReadOnlyList<TagPoint>>();

        foreach (var item in summary.Items)
        {
            double? value = null;
            string detailsJson = "{}";

            if (item.SourceType == SourceType.Constant)
            {
                value = item.ConstantValue;
                detailsJson = JsonSerializer.Serialize(new { source = "Constant", value });
            }
            else if (item.SourceType == SourceType.QualityIsOk)
            {
                var isOk = await _results.GetQualityIsOkAsync(run.RunId, ct);
                value = isOk is null ? null : (isOk.Value ? 1.0 : 0.0);
                detailsJson = JsonSerializer.Serialize(new { source = "QualityIsOk", isOk });
            }
            else // Tag
            {
                if (item.TagNodeId is null || item.Scope is null || item.Aggregation is null)
                    throw new InvalidOperationException($"Summary item '{item.FieldName}' is Tag source but TagNodeId/Scope/Aggregation is missing.");

                var (val, det) = await _agg.ComputeAsync(
                    run,
                    item.TagNodeId,
                    item.Scope.Value,
                    item.Aggregation.Value,
                    item.LookbackMinutes,
                    item.MaxGapSeconds,
                    rangeMin: null,
                    rangeMax: null,
                    ct,
                    cache);

                value = val;
                detailsJson = det;
            }

            await _results.UpsertSummaryValueAsync(run.RunId, item.FieldName, value, item.Unit, detailsJson, ct);
            _log.LogInformation("Summary computed: RunId={RunId} {Field}={Value}", run.RunId, item.FieldName, value);
        }

        await _results.MarkModuleComputedAsync(run.RunId, module: "SUMMARY", error: null, ct);
    }
}
