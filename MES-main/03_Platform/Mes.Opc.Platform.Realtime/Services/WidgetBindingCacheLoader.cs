using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mes.Opc.Platform.Data.Repository;
using Microsoft.Extensions.DependencyInjection;

namespace Mes.Opc.Platform.Realtime.Services
{
    /// <summary>
    /// Hosted service that loads UiWidgetBinding records from SQL at startup
    /// and builds the in-memory routing map used for realtime widget updates.
    ///
    /// For the MVP we load once at startup. Later, you can extend this to
    /// refresh periodically or when the UI configuration changes.
    /// </summary>
    public sealed class WidgetBindingCacheLoader : IHostedService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly WidgetBindingCache _cache;
        private readonly ILogger<WidgetBindingCacheLoader> _logger;

        public WidgetBindingCacheLoader(
            IServiceScopeFactory scopeFactory,
            WidgetBindingCache cache,
            ILogger<WidgetBindingCacheLoader> logger)
        {
            _scopeFactory = scopeFactory;
            _cache = cache;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<UiConfigurationRepository>();
                var rows = await repo.GetAllWidgetBindingsAsync().ConfigureAwait(false);
                var map = new Dictionary<(string Machine, string NodeId), List<(Guid WidgetId, string Role)>>();

                foreach (var r in rows)
                {
                    var key = (Normalize(r.MachineCode), Normalize(r.OpcNodeId));
                    if (!map.TryGetValue(key, out var list))
                    {
                        list = new List<(Guid, string)>();
                        map[key] = list;
                    }
                    list.Add((r.WidgetId, r.BindingRole));
                }

                _cache.Replace(map);
                _logger.LogInformation("Widget binding cache loaded: {Bindings} bindings, {Routes} routes", rows.Count, map.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load widget bindings from SQL. Widget realtime updates will be empty.");
                _cache.Replace(new Dictionary<(string, string), List<(Guid, string)>>());
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private static string Normalize(string? s) => (s ?? string.Empty).Trim().ToUpperInvariant();
    }
}
