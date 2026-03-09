using System.Text.Json;
using Mes.Opc.Platform.AnalyticsWorker.Data;
using Mes.Opc.Platform.AnalyticsWorker.Domain;
using Microsoft.Extensions.Logging;

namespace Mes.Opc.Platform.AnalyticsWorker.Engine;

public sealed class KpiEngine
{
    private readonly TagAggregationEngine _agg;
    private readonly ResultRepository _results;
    private readonly ILogger<KpiEngine> _log;

    public KpiEngine(TagAggregationEngine agg, ResultRepository results, ILogger<KpiEngine> log)
    {
        _agg = agg;
        _results = results;
        _log = log;
    }

    public async Task ComputeAndStoreIfNeededAsync(ProductionRunRow run, MachineConfig cfg, CancellationToken ct)
    {
        var status = await _results.GetRunStatusAsync(run.RunId, ct);
        if (status.KpiComputedAt is not null)
        {
            _log.LogDebug("Skip KPI for RunId={RunId} (already computed at {At})", run.RunId, status.KpiComputedAt);
            return;
        }

        var cache = new Dictionary<string, IReadOnlyList<TagPoint>>();

        foreach (var kpi in cfg.Kpis.Where(k => k.IsActive))
        {
            var variables = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            var varDetails = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            foreach (var v in kpi.Variables)
            {
                object? computed = null;
                string detailsJson = "{}";

                if (v.SourceType == SourceType.Constant)
                {
                    computed = v.ConstantValue;
                    detailsJson = JsonSerializer.Serialize(new { source = "Constant", value = v.ConstantValue });
                }
                else if (v.SourceType == SourceType.QualityIsOk)
                {
                    var isOk = await _results.GetQualityIsOkAsync(run.RunId, ct);
                    computed = isOk is null ? null : (isOk.Value ? 1.0 : 0.0);
                    detailsJson = JsonSerializer.Serialize(new { source = "QualityIsOk", isOk });
                }
                else // Tag
                {
                    if (v.TagNodeId is null || v.Scope is null || v.Aggregation is null)
                        throw new InvalidOperationException($"KPI variable '{v.VariableName}' is Tag source but TagNodeId/Scope/Aggregation is missing.");

                    var (val, det) = await _agg.ComputeAsync(
                        run,
                        v.TagNodeId,
                        v.Scope.Value,
                        v.Aggregation.Value,
                        v.LookbackMinutes,
                        v.MaxGapSeconds,
                        v.RangeMin,
                        v.RangeMax,
                        ct,
                        cache);

                    computed = val;
                    detailsJson = det;
                }

                // Missing data handling
                if (computed is null)
                {
                    switch (v.MissingDataMode)
                    {
                        case MissingDataMode.UseZero:
                            computed = 0.0;
                            break;
                        case MissingDataMode.Fail:
                            throw new InvalidOperationException($"Missing data for KPI='{kpi.Code}' variable='{v.VariableName}'.");
                        case MissingDataMode.NullResult:
                        default:
                            // keep null
                            break;
                    }
                }

                variables[v.VariableName] = computed;
                varDetails[v.VariableName] = new { value = computed, detailsJson };
            }

            // If any variable is null and MissingDataMode was NullResult, we return KPI = NULL (by convention).
            var anyNull = variables.Any(kv => kv.Value is null);
            double? kpiValue = null;
            string? evalError = null;

            if (!anyNull)
            {
                kpiValue = ExpressionEvaluator.TryEvaluate(kpi.Expression, variables, out evalError);
            }

            var resultDetails = JsonSerializer.Serialize(new
            {
                kpi = new { kpi.Code, kpi.Name, kpi.Expression, kpi.Unit },
                variables = varDetails,
                evaluationError = evalError,
                note = anyNull ? "At least one variable is NULL -> KPI result is NULL (MissingDataMode=NullResult)." : null
            });

            await _results.UpsertKpiResultAsync(run.RunId, kpi.Code, kpiValue, kpi.Unit, resultDetails, ct);

            _log.LogInformation("KPI computed: RunId={RunId} {KpiCode}={Value}", run.RunId, kpi.Code, kpiValue);
        }

        await _results.MarkModuleComputedAsync(run.RunId, module: "KPI", error: null, ct);
    }
}
