// -------------------------------------------------------------------------------------------------
// MachineTagValueWriter.cs
//
// A hosted background service that consumes MachineTagValue entities from a
// channel and writes them to the database in batches.  Batching improves
// performance by reducing the number of round‑trips to the database.
// Detailed comments explain how the buffering and flushing logic works.
// -------------------------------------------------------------------------------------------------

using System;                                     // Provides DateTime and Exception types
using System.Collections.Generic;                 // Provides List<T> for buffering
using System.Linq;                                // Provides Any() extension method
using System.Threading;                            // Provides CancellationToken for cooperative cancellation
using System.Threading.Channels;                   // Provides Channel<T> for inter‑service communication
using System.Threading.Tasks;                      // Provides asynchronous programming constructs
using Mes.Opc.Acquisition.Runtime.Cycle;
using Mes.Opc.Acquisition.Runtime.Infrastructure; // Imports DbChannel wrapper
using Mes.Opc.Acquisition.Runtime.Persistence;    // Imports the MachineTagValue model
using Mes.Opc.Acquisition.Runtime.Repository;     // Imports the repository for database insertion
using Microsoft.Extensions.DependencyInjection;     // Provides service resolution within a scope
using Microsoft.Extensions.Hosting;               // Provides BackgroundService base class
using Microsoft.Extensions.Logging;               // Provides logging abstractions

namespace Mes.Opc.Acquisition.Runtime
{
    /// <summary>
    /// Reads MachineTagValue objects from a channel and periodically writes them to the
    /// database in batches.  The writer runs as a background service and stops when
    /// the host shuts down.
    /// </summary>
    public class MachineTagValueWriter : BackgroundService
    {
        private readonly Channel<MachineTagValue> _channel;  // The channel from which values are read
        private readonly IServiceScopeFactory _scopeFactory;  // Factory used to create a scope for repository resolution
        private readonly ILogger<MachineTagValueWriter> _logger; // Logger for diagnostic messages
        private readonly CycleTrackingOptions _cycleOptions;
        private readonly IProductRunTracker? _runTracker;

        /// <summary>
        /// Constructs a new <see cref="MachineTagValueWriter"/> instance.  Dependencies are
        /// injected via constructor injection by the DI container.
        /// </summary>
        /// <param name="dbChannel">The channel wrapper from which MachineTagValue entities are consumed.</param>
        /// <param name="scopeFactory">Factory for creating scopes to resolve scoped services.</param>
        /// <param name="logger">Logger used to record information and errors.</param>
        public MachineTagValueWriter(
            DbChannel dbChannel,
            IServiceScopeFactory scopeFactory,
            ILogger<MachineTagValueWriter> logger,
            Microsoft.Extensions.Options.IOptions<CycleTrackingOptions> cycleOptions,
            IProductRunTracker? runTracker = null)
        {
            _channel = dbChannel.Channel;
            _scopeFactory = scopeFactory;
            _logger = logger;
            _cycleOptions = cycleOptions.Value;
            _runTracker = runTracker;
        }

        /// <summary>
        /// Executes the background operation.  This method is called by the host when
        /// the service starts.  It reads items from the channel until cancellation
        /// is requested and flushes them to the database periodically.
        /// </summary>
        /// <param name="stoppingToken">A token signaled when the host is shutting down.</param>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<MachineTagValueRepository>();
            var runRepo = scope.ServiceProvider.GetService<IProductionRunRepository>();
            var buffer = new List<MachineTagValue>();
            var lastFlush = DateTime.UtcNow;
            await foreach (var item in _channel.Reader.ReadAllAsync(stoppingToken))
            {

                if (_cycleOptions.Enabled && _runTracker != null)
                {
                    var ts = item.SourceTimestamp?.ToUniversalTime() ?? DateTime.UtcNow;
                    var sample = new TagSample(item.MachineCode, item.OpcNodeId, item.Value, ts);
                    var result = _runTracker.Process(sample);
                    item.ProductionRunId = result.RunId;
                    if (result.Event != null && runRepo != null)
                    {
                        if (result.Event.Type == RunEventType.Started)
                        {
                            await runRepo.TryInsertStartedAsync(result.Event.RunId, result.Event.MachineCode, result.Event.ScopeKey, result.Event.TimestampUtc);
                        }
                        else
                        {
                            var status = result.Event.Type == RunEventType.Aborted ? "Aborted" : "Completed";
                            if (result.Event.Type == RunEventType.Timeout) status = "Timeout";
                            await runRepo.TryUpdateEndAsync(result.Event.RunId, result.Event.TimestampUtc, status, result.Event.Reason);
                        }
                    }
                }

                buffer.Add(item);
                var timeSinceLastFlush = DateTime.UtcNow - lastFlush;
                if (buffer.Count >= 100 || timeSinceLastFlush >= TimeSpan.FromSeconds(5))
                {
                    await FlushAsync(repo, buffer);
                    buffer.Clear();
                    lastFlush = DateTime.UtcNow;
                }
            }
            if (buffer.Any())
            {
                await FlushAsync(repo, buffer);
            }
        }

        /// <summary>
        /// Writes a batch of MachineTagValue records to the database using the repository.
        /// Errors are logged but not rethrown to ensure that the writer continues running.
        /// </summary>
        /// <param name="repo">The repository used to insert the batch.</param>
        /// <param name="batch">The batch of values to insert.</param>
        private async Task FlushAsync(
            MachineTagValueRepository repo,
            List<MachineTagValue> batch)
        {
            try
            {
                await repo.InsertBatchAsync(batch);
                _logger.LogInformation(
                    "OPC | DB flush completed | Count={count}",
                    batch.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "OPC | DB flush error");
            }
        }
    }
}