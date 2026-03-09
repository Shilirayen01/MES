using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Mes.Opc.Acquisition.Runtime.Infrastructure;
using Mes.Opc.Contracts.Dtos;
using Mes.Opc.Acquisition.Runtime.Persistence;

namespace Mes.Opc.Platform.Realtime.Services
{
    /// <summary>
    /// Converts realtime OPC tag updates (MachineTagValue) into widget-targeted
    /// updates (WidgetUpdateDto) using the WidgetBindingCache.
    ///
    /// This keeps the UI independent of OPC details and reduces client-side routing.
    /// </summary>
    public sealed class WidgetValueDispatcher
    {
        private readonly WidgetBindingCache _cache;
        private readonly IHubContext<OpcTagHub> _hub;

        public WidgetValueDispatcher(WidgetBindingCache cache, IHubContext<OpcTagHub> hub)
        {
            _cache = cache;
            _hub = hub;
        }

        public async Task DispatchAsync(MachineTagValue value, CancellationToken ct)
        {
            var targets = _cache.Resolve(value.MachineCode, value.OpcNodeId);
            if (targets.Count == 0)
                return;

            foreach (var (widgetId, role) in targets)
            {
                var dto = new WidgetUpdateDto
                {
                    WidgetId = widgetId,
                    BindingRole = role,
                    Value = value.Value?.ToString(),
                    TimestampUtc = DateTime.UtcNow
                };

                await _hub.Clients
                    .Group($"widget-{widgetId}")
                    .SendAsync("widgetUpdate", dto, cancellationToken: ct)
                    .ConfigureAwait(false);
            }
        }
    }
}
