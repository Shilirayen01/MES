using System.Text.Json;
using Mes.Opc.Platform.AnalyticsWorker.Data;
using Mes.Opc.Platform.AnalyticsWorker.Domain;
using Mes.Opc.Platform.AnalyticsWorker.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mes.Opc.Platform.AnalyticsWorker.Engine;

public sealed class QualityEngine
{
    private readonly TagValueRepository _tags;
    private readonly ResultRepository _results;
    private readonly AnalyticsWorkerOptions _opt;
    private readonly ILogger<QualityEngine> _log;

    public QualityEngine(
        TagValueRepository tags,
        ResultRepository results,
        IOptions<AnalyticsWorkerOptions> options,
        ILogger<QualityEngine> log)
    {
        _tags = tags;
        _results = results;
        _opt = options.Value;
        _log = log;
    }

    public async Task ComputeAndStoreIfNeededAsync(ProductionRunRow run, MachineConfig cfg, CancellationToken ct)
    {
        var status = await _results.GetRunStatusAsync(run.RunId, ct);
        if (status.QualityComputedAt is not null)
        {
            _log.LogDebug("Skip Quality for RunId={RunId} (already computed at {At})", run.RunId, status.QualityComputedAt);
            return;
        }

        var rs = cfg.QualityRuleSet;
        if (rs is null || !rs.IsActive || rs.Rules.Count == 0)
        {
            await _results.UpsertQualityAsync(run.RunId, isOk: null, reasonJson: JsonSerializer.Serialize(new { note = "No active quality rule set." }), ct);
            await _results.MarkModuleComputedAsync(run.RunId, module: "QUALITY", error: null, ct);
            return;
        }

        var cache = new Dictionary<string, IReadOnlyList<TagPoint>>();
        var evaluations = new List<object>();
        var passes = new List<bool>();

        foreach (var rule in rs.Rules.Where(r => r.IsActive))
        {
            var lb = rule.LookbackMinutes ?? _opt.DefaultLookbackMinutes;
            var mg = rule.MaxGapSeconds ?? _opt.DefaultMaxGapSeconds;

            var key = $"{rule.Scope}|{rule.TagNodeId}|lb={lb}";
            if (!cache.TryGetValue(key, out var points))
            {
                points = await _tags.GetDedupedPointsAsync(run, rule.TagNodeId, rule.Scope, lb, ct);
                cache[key] = points;
            }

            var window = points.Where(p => p.Timestamp >= run.StartTs && p.Timestamp <= run.EndTs).ToList();
            bool pass;
            double? evidenceValue = null;
            double? evidencePercent = null;

            switch (rule.EvaluationMode)
            {
                case RuleEvaluationMode.AsOfEnd:
                    evidenceValue = AsOf(points, run.EndTs);
                    pass = evidenceValue is not null && IsConditionMet(evidenceValue.Value, rule);
                    break;

                case RuleEvaluationMode.MaxValue:
                    evidenceValue = window.Count == 0 ? null : window.Max(p => p.Value);
                    pass = evidenceValue is not null && IsConditionMet(evidenceValue.Value, rule);
                    break;

                case RuleEvaluationMode.MinValue:
                    evidenceValue = window.Count == 0 ? null : window.Min(p => p.Value);
                    pass = evidenceValue is not null && IsConditionMet(evidenceValue.Value, rule);
                    break;

                case RuleEvaluationMode.PercentInRange:
                    evidencePercent = ComputePercentInRange(points, run.StartTs, run.EndTs, mg, rule.MinValue, rule.MaxValue);
                    if (evidencePercent is null || rule.PercentThreshold is null)
                        pass = false;
                    else
                        pass = evidencePercent.Value >= rule.PercentThreshold.Value;
                    break;

                case RuleEvaluationMode.AnyViolation:
                default:
                    if (window.Count == 0)
                    {
                        pass = false;
                    }
                    else
                    {
                        // pass if no violation
                        pass = !window.Any(p => !IsConditionMet(p.Value, rule));
                        evidenceValue = window.Count == 0 ? null : window.Last().Value;
                    }
                    break;
            }

            passes.Add(pass);

            evaluations.Add(new
            {
                rule.Code,
                rule.TagNodeId,
                scope = rule.Scope.ToString(),
                rule.Condition,
                rule.EvaluationMode,
                expected = rule.ExpectedValue,
                min = rule.MinValue,
                max = rule.MaxValue,
                percentThreshold = rule.PercentThreshold,
                evidenceValue,
                evidencePercent,
                pass,
                windowPoints = window.Count,
                asOfEnd = AsOf(points, run.EndTs)
            });
        }

        bool? isOk;
        if (passes.Count == 0)
        {
            isOk = null;
        }
        else
        {
            isOk = rs.LogicMode == QualityLogicMode.And
                ? passes.All(x => x)
                : passes.Any(x => x);
        }

        var reason = JsonSerializer.Serialize(new
        {
            ruleSet = new { rs.RuleSetId, rs.Name, rs.LogicMode },
            isOk,
            evaluations
        });

        await _results.UpsertQualityAsync(run.RunId, isOk, reason, ct);
        await _results.MarkModuleComputedAsync(run.RunId, module: "QUALITY", error: null, ct);

        _log.LogInformation("Quality computed: RunId={RunId} IsOk={IsOk}", run.RunId, isOk);
    }

    private static double? AsOf(IReadOnlyList<TagPoint> points, DateTime boundary)
    {
        for (var i = points.Count - 1; i >= 0; i--)
        {
            if (points[i].Timestamp <= boundary)
                return points[i].Value;
        }
        return null;
    }

    private static bool IsConditionMet(double value, QualityRule rule)
    {
        return rule.Condition switch
        {
            ConditionType.Equals => rule.ExpectedValue is not null && value == rule.ExpectedValue.Value,
            ConditionType.NotEquals => rule.ExpectedValue is not null && value != rule.ExpectedValue.Value,

            ConditionType.InRange => rule.MinValue is not null && rule.MaxValue is not null && value >= rule.MinValue.Value && value <= rule.MaxValue.Value,
            ConditionType.OutOfRange => rule.MinValue is not null && rule.MaxValue is not null && (value < rule.MinValue.Value || value > rule.MaxValue.Value),

            ConditionType.GreaterThan => rule.ExpectedValue is not null && value > rule.ExpectedValue.Value,
            ConditionType.GreaterOrEqual => rule.ExpectedValue is not null && value >= rule.ExpectedValue.Value,
            ConditionType.LessThan => rule.ExpectedValue is not null && value < rule.ExpectedValue.Value,
            ConditionType.LessOrEqual => rule.ExpectedValue is not null && value <= rule.ExpectedValue.Value,

            _ => false
        };
    }

    private static double? ComputePercentInRange(
        IReadOnlyList<TagPoint> points,
        DateTime start,
        DateTime end,
        int maxGapSeconds,
        double? rangeMin,
        double? rangeMax)
    {
        if (rangeMin is null || rangeMax is null) return null;

        var startVal = AsOf(points, start);
        if (startVal is null) return null;

        var timeline = new List<TagPoint>(capacity: points.Count + 1) { new(start, startVal.Value) };
        timeline.AddRange(points.Where(p => p.Timestamp > start && p.Timestamp <= end));
        if (timeline.Count < 2) return null;

        double inRangeMs = 0;
        double totalMs = 0;

        for (int i = 0; i < timeline.Count; i++)
        {
            var t0 = timeline[i].Timestamp;
            var v0 = timeline[i].Value;
            var t1 = (i + 1 < timeline.Count) ? timeline[i + 1].Timestamp : end;
            if (t1 > end) t1 = end;

            var dt = (t1 - t0).TotalMilliseconds;
            if (dt <= 0) continue;

            if (maxGapSeconds > 0 && dt > maxGapSeconds * 1000.0)
                return null;

            totalMs += dt;
            if (v0 >= rangeMin.Value && v0 <= rangeMax.Value)
                inRangeMs += dt;

            if (t1 >= end) break;
        }

        if (totalMs <= 0) return null;
        return (inRangeMs / totalMs) * 100.0;
    }
}
