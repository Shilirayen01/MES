using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mes.Opc.Acquisition.Runtime.Cycle
{
    public sealed class CycleRuleReloadService : BackgroundService
    {
        private readonly CycleRuleRepository _repo;
        private readonly CycleRuleStore _store;
        private readonly CycleTrackingOptions _options;
        private readonly ILogger<CycleRuleReloadService> _logger;

        public CycleRuleReloadService(
            CycleRuleRepository repo,
            CycleRuleStore store,
            IOptions<CycleTrackingOptions> options,
            ILogger<CycleRuleReloadService> logger)
        {
            _repo = repo;
            _store = store;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation("CycleTracking disabled");
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var rules = await _repo.LoadActiveRulesAsync();
                    _store.Swap(rules);
                    _logger.LogInformation("CycleTracking rules reloaded | Count={count}", rules.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "CycleTracking rule reload error");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, _options.RuleReloadSeconds)), stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }
    }
}
