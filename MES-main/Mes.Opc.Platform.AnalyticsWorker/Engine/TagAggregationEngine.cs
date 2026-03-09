using System.Text.Json;
using Mes.Opc.Platform.AnalyticsWorker.Data;
using Mes.Opc.Platform.AnalyticsWorker.Domain;
using Mes.Opc.Platform.AnalyticsWorker.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mes.Opc.Platform.AnalyticsWorker.Engine;

public sealed class TagAggregationEngine
{
    private readonly TagValueRepository _tags;
    private readonly AnalyticsWorkerOptions _opt;
    private readonly ILogger<TagAggregationEngine> _log;

    public TagAggregationEngine(
        TagValueRepository tags,
        IOptions<AnalyticsWorkerOptions> options,
        ILogger<TagAggregationEngine> log)
    {
        _tags = tags;
        _opt = options.Value;
        _log = log;
    }

    /// <summary>
    /// Computes an aggregation over a tag for a given ProductionRun.
    /// Implements TS rules: dedup by SourceTimestamp, as-of boundaries, lookback, max gap, scope decision.
    /// </summary>
    public async Task<(double? Value, string DetailsJson)> ComputeAsync(
        ProductionRunRow run,
        string tagNodeId,
        TagScope scope,
        AggregationType aggregation,
        int? lookbackMinutes,
        int? maxGapSeconds,
        double? rangeMin,
        double? rangeMax,
        CancellationToken ct,
        Dictionary<string, IReadOnlyList<TagPoint>> cache)
    {
        var lb = lookbackMinutes ?? _opt.DefaultLookbackMinutes;
        var mg = maxGapSeconds ?? _opt.DefaultMaxGapSeconds;

        var key = $"{scope}|{tagNodeId}|lb={lb}";
        if (!cache.TryGetValue(key, out var points))
        {
            points = await _tags.GetDedupedPointsAsync(run, tagNodeId, scope, lb, ct);
            cache[key] = points;
        }

        // window points
        var window = points.Where(p => p.Timestamp >= run.StartTs && p.Timestamp <= run.EndTs).ToList();

        double? result = aggregation switch
        {
            AggregationType.Min => window.Count == 0 ? null : window.Min(p => p.Value),
            AggregationType.Max => window.Count == 0 ? null : window.Max(p => p.Value),
            AggregationType.Avg => window.Count == 0 ? null : window.Average(p => p.Value),

            AggregationType.AsOfStart => AsOf(points, run.StartTs),
            AggregationType.AsOfEnd => AsOf(points, run.EndTs),
            AggregationType.LastAsOfEnd => AsOf(points, run.EndTs),

            AggregationType.Delta => ComputeDelta(points, run.StartTs, run.EndTs),
            AggregationType.TimeWeightedAverage => ComputeTwa(points, run.StartTs, run.EndTs, mg),
            AggregationType.PercentInRange => ComputePercentInRange(points, run.StartTs, run.EndTs, mg, rangeMin, rangeMax),

            _ => throw new ArgumentOutOfRangeException(nameof(aggregation), aggregation, "Unsupported aggregation type.")
        };

        var details = new
        {
            tagNodeId,
            scope = scope.ToString(),
            aggregation = aggregation.ToString(),
            lookbackMinutes = lb,
            maxGapSeconds = mg,
            startTs = run.StartTs,
            endTs = run.EndTs,
            totalPoints = points.Count,
            windowPoints = window.Count,
            asOfStart = AsOf(points, run.StartTs),
            asOfEnd = AsOf(points, run.EndTs),
            rangeMin,
            rangeMax,
            result
        };

        return (result, JsonSerializer.Serialize(details));
    }

    private static double? AsOf(IReadOnlyList<TagPoint> points, DateTime boundary)
    {
        // last point <= boundary
        for (var i = points.Count - 1; i >= 0; i--)
        {
            if (points[i].Timestamp <= boundary)
                return points[i].Value;
        }
        return null;
    }

    private static double? ComputeDelta(IReadOnlyList<TagPoint> points, DateTime start, DateTime end)
    {
        var s = AsOf(points, start);
        var e = AsOf(points, end);
        if (s is null || e is null) return null;
        return e.Value - s.Value;
    }

    private static double? ComputeTwa(IReadOnlyList<TagPoint> points, DateTime start, DateTime end, int maxGapSeconds)
    {
        var startVal = AsOf(points, start);
        if (startVal is null) return null;

        // Build timeline: (start, startVal) + all points inside (start,end]
        var timeline = new List<TagPoint>(capacity: points.Count + 1) { new(start, startVal.Value) };
        timeline.AddRange(points.Where(p => p.Timestamp > start && p.Timestamp <= end));

        if (timeline.Count < 2)
        {
            // only start point -> no duration information
            return null;
        }

        double weighted = 0;
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
            {
                // TS: if a gap is too large, config can cut or mark insufficient.
                // Here we choose "insufficient data" to avoid wrong averages.
                return null;
            }

            weighted += v0 * dt;
            totalMs += dt;

            if (t1 >= end) break;
        }

        if (totalMs <= 0) return null;
        return weighted / totalMs;
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
        return (inRangeMs / totalMs) * 100.0; // percent 0..100
    }
}
