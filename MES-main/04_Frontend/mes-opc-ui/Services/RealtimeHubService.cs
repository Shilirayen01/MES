using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Mes.Opc.Contracts.Dtos;
using static System.Formats.Asn1.AsnWriter;

namespace mes_opc_ui.Services
{
    /// <summary>
    /// Manages a SignalR connection to /opcHub.
    ///
    /// Responsibilities:
    /// - Start the connection
    /// - Join machine groups based on UI configuration
    /// - Forward incoming tagUpdate messages to WidgetValueStore
    /// </summary>
    public sealed class RealtimeHubService : IAsyncDisposable
    {
        private HubConnection? _connection;
        private readonly WidgetValueStore _store;

        /// <summary>
        /// Raised whenever SignalR changes state (connected/reconnecting/closed), so the UI can refresh.
        /// </summary>
        public event Action? StateChanged;

        /// <summary>
        /// Last connection error (if any). Useful for showing a short badge in the UI.
        /// </summary>
        public string? LastError { get; private set; }

        public RealtimeHubService(WidgetValueStore store)
        {
            _store = store;
        }

        public bool IsConnected => _connection?.State == HubConnectionState.Connected;
        public event Action<WidgetUpdateDto>? WidgetUpdated;

        public async Task StartAsync(string baseUrl, CancellationToken ct = default)
        {
            // Hub URL
            var hubUrl = new Uri(new Uri(baseUrl), "opcHub");

            // If a previous attempt failed, we may have a disconnected connection instance.
            // In that case, allow retry by starting again.
            if (_connection is null)
            {
                _connection = new HubConnectionBuilder()
                    .ConfigureLogging(logging =>
                    {
                        // Shows useful details in the browser console when connections fail.
                        logging.SetMinimumLevel(LogLevel.Information);
                    })
                    // WebAssembly + cross-origin is most reliable with WebSockets-only.
                    // This also avoids negotiate/preflight surprises.
                    .WithUrl(hubUrl, options =>
                    {
                        options.Transports =
                            HttpTransportType.WebSockets |
                            HttpTransportType.ServerSentEvents |
                            HttpTransportType.LongPolling;
                        options.SkipNegotiation = false;
                    })
                    .WithAutomaticReconnect()
                    .Build();

                // State hooks for UI diagnostics
                _connection.Reconnecting += ex =>
                {
                    LastError = ex?.GetType().Name;
                    StateChanged?.Invoke();
                    return Task.CompletedTask;
                };
                _connection.Reconnected += _ =>
                {
                    LastError = null;
                    StateChanged?.Invoke();
                    return Task.CompletedTask;
                };
                _connection.Closed += ex =>
                {
                    LastError = ex?.GetType().Name;
                    StateChanged?.Invoke();
                    return Task.CompletedTask;
                };

                _connection.On<MachineTagValueDto>("tagUpdate", tag => _store.OnTagUpdate(tag));
                // Preferred: per-widget updates (server resolves bindings and routes by WidgetId)
                _connection.On<WidgetUpdateDto>("widgetUpdate", dto =>
                {
                    Console.WriteLine($"[UI] widgetUpdate id={dto.WidgetId} role={dto.BindingRole} value={dto.Value}");
                    WidgetUpdated?.Invoke(dto);
                });


            }

            if (_connection.State is HubConnectionState.Connected or HubConnectionState.Connecting or HubConnectionState.Reconnecting)
                return;

            try
            {
                await _connection.StartAsync(ct);
                LastError = null;
                StateChanged?.Invoke();
            }
            catch
            {
                LastError = "StartFailed";
                StateChanged?.Invoke();
                // If start fails, dispose and allow a clean retry next time.
                try { await _connection.DisposeAsync(); } catch { /* ignore */ }
                _connection = null;
                throw;
            }
        }

        public async Task JoinMachinesAsync(IEnumerable<string> machineCodes, CancellationToken ct = default)
        {
            if (_connection is null)
                return;

            foreach (var code in machineCodes)
            {
                if (string.IsNullOrWhiteSpace(code))
                    continue;
                await _connection.InvokeAsync("JoinMachine", code, ct);
            }
        }

        public async Task JoinWidgetsAsync(IEnumerable<Guid> widgetIds, CancellationToken ct = default)
        {
            if (_connection is null)
                return;

            foreach (var id in widgetIds)
            {
                if (id == Guid.Empty)
                    continue;
                await _connection.InvokeAsync("JoinWidget", id, ct);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_connection is null)
                return;

            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
