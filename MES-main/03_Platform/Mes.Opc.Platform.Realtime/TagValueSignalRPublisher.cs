// -------------------------------------------------------------------------------------------------
// TagValueSignalRPublisher.cs
//
// Background service that consumes tag values from the real‑time channel
// and pushes them to SignalR clients.  Each value is sent to the group
// corresponding to the machine code of the tag.  This service is registered
// as a hosted service in Program.cs.
// -------------------------------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mes.Opc.Acquisition.Runtime.Infrastructure;
using Mes.Opc.Acquisition.Runtime.Persistence;
using Mes.Opc.Platform.Realtime.Services;

namespace Mes.Opc.Platform.Realtime
{
    /// <summary>
    /// Consumes <see cref="MachineTagValue"/> instances from the <see cref="RtChannel"/>
    /// and publishes them to connected SignalR clients.  Each tag value is
    /// broadcast to the group named after the machine code.  If the hub
    /// operation throws an exception the error is logged and the value
    /// discarded.
    /// </summary>
    public sealed class TagValueSignalRPublisher : BackgroundService
    {
        private readonly RtChannel _rtChannel;
        private readonly IHubContext<OpcTagHub> _hubContext;
        private readonly WidgetValueDispatcher _widgetDispatcher;
        private readonly ILogger<TagValueSignalRPublisher> _logger;

        /// <summary>
        /// Initialises a new instance of the <see cref="TagValueSignalRPublisher"/> class.
        /// </summary>
        /// <param name="rtChannel">The real‑time channel from which tag values are read.</param>
        /// <param name="hubContext">The SignalR hub context used to broadcast updates.</param>
        /// <param name="logger">Logger for diagnostic messages and errors.</param>
        public TagValueSignalRPublisher(
            RtChannel rtChannel,
            IHubContext<OpcTagHub> hubContext,
            WidgetValueDispatcher widgetDispatcher,
            ILogger<TagValueSignalRPublisher> logger)
        {
            _rtChannel = rtChannel;
            _hubContext = hubContext;
            _widgetDispatcher = widgetDispatcher;
            _logger = logger;
        }

        /// <summary>
        /// Executes the background service.  Continuously reads values from the
        /// real‑time channel and broadcasts them to the appropriate SignalR
        /// group.  Cancellation is monitored via the provided token.
        /// </summary>
        /// <param name="stoppingToken">Token that signals when the host is shutting down.</param>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var reader = _rtChannel.Channel.Reader;
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var value = await reader.ReadAsync(stoppingToken).ConfigureAwait(false);
                    var group = value.MachineCode.Trim().ToUpperInvariant();
                    await _hubContext.Clients.Group(group).SendAsync("tagUpdate", value, cancellationToken: stoppingToken).ConfigureAwait(false);

                    // Additionally push a widget-targeted update so the UI can subscribe per widget.
                    await _widgetDispatcher.DispatchAsync(value, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Cancellation requested: exit gracefully
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending tag update over SignalR");
                }
            }
        }
    }
}