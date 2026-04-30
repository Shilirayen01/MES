using Mes.Opc.Platform.AnalyticsWorker.Data;
using Mes.Opc.Platform.AnalyticsWorker.Engine;
using Mes.Opc.Platform.AnalyticsWorker.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mes.Opc.Platform.AnalyticsWorker;

public sealed class AnalyticsWorker : BackgroundService
{
    private readonly ILogger<AnalyticsWorker> _log;
    private readonly AnalyticsWorkerOptions _opt;
    private readonly ProductionRunRepository _runs;
    private readonly ConfigRepository _config;
    private readonly ResultRepository _results;
    private readonly QualityEngine _quality;
    private readonly KpiEngine _kpi;
    private readonly SummaryEngine _summary;

    public AnalyticsWorker(
        ILogger<AnalyticsWorker> log,
        IOptions<AnalyticsWorkerOptions> options,
        ProductionRunRepository runs,
        ConfigRepository config,
        ResultRepository results,
        QualityEngine quality,
        KpiEngine kpi,
        SummaryEngine summary)
    {
        _log = log;
        _opt = options.Value;
        _runs = runs;
        _config = config;
        _results = results;
        _quality = quality;
        _kpi = kpi;
        _summary = summary;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("AnalyticsWorker started. PollInterval={PollSeconds}s BatchSize={BatchSize}",
            _opt.PollIntervalSeconds, _opt.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOnce(stoppingToken);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Unhandled error in worker loop");
            }

            await Task.Delay(TimeSpan.FromSeconds(_opt.PollIntervalSeconds), stoppingToken);
        }
    }

    private async Task ProcessOnce(CancellationToken ct)
    {
        var runs = await _runs.GetCompletedRunsNeedingProcessingAsync(
            batchSize: _opt.BatchSize,
            maxLookbackDays: _opt.MaxLookbackDays,
            ct);

        if (runs.Count == 0)
        {
            _log.LogDebug("No runs to process.");
            return;
        }

        _log.LogInformation("Found {Count} completed run(s) needing processing.", runs.Count);

        foreach (var run in runs)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                // Load all configuration for the machine once.
                var machineCfg = await _config.LoadMachineConfigAsync(run.MachineCode, ct);

                // 1) Quality rules (produces ProductionRunQuality.IsOk) - used by FTQ or any KPI expression referencing IsOk.
                if (machineCfg.QualityRuleSet is not null)
                {
                    await _quality.ComputeAndStoreIfNeededAsync(run, machineCfg, ct);
                }

                // 2) KPI engine
                if (machineCfg.Kpis.Count > 0)
                {
                    await _kpi.ComputeAndStoreIfNeededAsync(run, machineCfg, ct);
                }

                // 3) Summary engine
                if (machineCfg.Summary is not null)
                {
                    await _summary.ComputeAndStoreIfNeededAsync(run, machineCfg, ct);
                }

                await _results.MarkRunProcessedAsync(run.RunId,
                    kpiDone: true,     // Aucune config = rien à calculer = terminé
                    summaryDone: true, // Aucune config = rien à calculer = terminé
                    qualityDone: true, // Aucune config = rien à calculer = terminé
                    error: null,
                    ct);

                _log.LogInformation("Run {RunId} processed OK (Machine={MachineCode}).", run.RunId, run.MachineCode);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Run {RunId} failed (Machine={MachineCode}).", run.RunId, run.MachineCode);

                await _results.MarkRunProcessedAsync(run.RunId,
                    kpiDone: false,
                    summaryDone: false,
                    qualityDone: false,
                    error: ex.Message,
                    ct);
            }
        }
    }
}
