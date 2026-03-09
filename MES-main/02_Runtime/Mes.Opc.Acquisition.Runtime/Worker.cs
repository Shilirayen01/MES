// -------------------------------------------------------------------------------------------------
// Worker.cs
//
// The Worker class orchestrates OPC UA endpoint runners.  When started by the
// generic host it loads configuration from the database, constructs and
// starts an OpcEndpointRunner for each configured OPC endpoint and keeps
// those runners alive until the application is shut down.  The worker also
// supports dynamic reload of configuration at a regular interval, allowing
// operators to add or remove machines and tags without restarting the
// service.  Every section of this file is documented to make the logic
// clear.
// -------------------------------------------------------------------------------------------------

using System;                                         // Provides basic types such as String and DateTime
using System.Collections.Generic;                     // Provides generic collection types like List<T>
using System.Linq;                                    // Provides LINQ extension methods for working with collections
using System.Threading;                               // Provides CancellationToken for graceful shutdown
using System.Threading.Tasks;                         // Provides Task and asynchronous helpers
using Mes.Opc.Acquisition.Runtime.Configuration;     // Imports configuration loader and model types
using Mes.Opc.Acquisition.Runtime.Infrastructure;     // Imports the tag value bus interface
using Mes.Opc.Acquisition.Runtime.Opc;               // Imports OPC session and subscription managers
using Microsoft.Extensions.DependencyInjection;        // Provides extension methods for resolving services from a scope
using Microsoft.Extensions.Hosting;                  // Provides the BackgroundService base class for hosted services
using Microsoft.Extensions.Logging;                  // Provides logging abstractions

namespace Mes.Opc.Acquisition.Runtime
{
    /// <summary>
    /// Worker orchestrates OPC UA endpoint runners.  It is implemented as a
    /// BackgroundService, meaning the host will call ExecuteAsync when the
    /// service starts and will manage cancellation when the application is
    /// shutting down.  The worker supports dynamic configuration reload
    /// so that changes to endpoints, machines or tag mappings are applied
    /// without restarting the service.
    /// </summary>
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;             // Logger used for diagnostic and informational messages
        private readonly IServiceScopeFactory _scopeFactory;  // Factory used to create scopes for retrieving scoped services
        private readonly ITagValueBus _bus;    // Bus used to publish MachineTagValue entities to channels
        private readonly ILoggerFactory _loggerFactory;        // LoggerFactory used to create loggers for dynamically created runners

        // ---------------------------------------------------------------------
        // Dynamic reload state
        //
        // To support reloading configuration without restarting the service we
        // keep track of the currently running runners in a dictionary keyed by
        // the endpoint ID.  Each entry stores the endpoint configuration,
        // the list of subscribed nodes, the runner instance, its cancellation
        // token source and the task returned by RunAsync.  When configuration
        // is reloaded we reconcile the new configuration with the existing
        // runners: starting runners for new endpoints, stopping those for
        // removed endpoints and restarting those whose subscribed nodes or
        // endpoint URL have changed.
        private readonly Dictionary<int, RunnerInfo> _runners = new();

        // The interval at which configuration is reloaded.  A shorter interval
        // improves responsiveness to changes but consumes more database and
        // CPU resources.  This can be made configurable via appsettings if
        // desired.
        private readonly TimeSpan _reloadInterval = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Holds information about a running OPC endpoint runner.  This nested
        /// type encapsulates the pieces required to manage a runner, including
        /// the endpoint configuration, the list of nodes being monitored, the
        /// runner instance itself, a CancellationTokenSource for signalling it
        /// to stop independently of the host token, and the Task representing
        /// its asynchronous execution.
        /// </summary>
        private class RunnerInfo
        {
            public Mes.Opc.Acquisition.Runtime.Configuration.OpcConfigModels.OpcEndpointConfig Endpoint { get; init; } = default!;
            public List<(string MachineCode, string NodeId)> Nodes { get; init; } = default!;
            public Mes.Opc.Acquisition.Runtime.Implementation.OpcEndpointRunner Runner { get; init; } = default!;
            public CancellationTokenSource Cancellation { get; init; } = default!;
            public Task Task { get; init; } = default!;
        }

        /// <summary>
        /// Compares two collections of node tuples for equality.  The comparison
        /// treats the collections as sets: order is ignored and duplicates
        /// are collapsed.  Machine codes are compared case‑insensitively and
        /// whitespace is trimmed, matching the semantics used when building
        /// the collections.  Node identifiers are compared case‑sensitively
        /// because they are opaque identifiers within the OPC UA address space.
        /// </summary>
        /// <param name="first">First collection of nodes.</param>
        /// <param name="second">Second collection of nodes.</param>
        /// <returns>True if the two collections contain the same elements; otherwise false.</returns>
        private static bool NodeSetsEqual(
            IList<(string MachineCode, string NodeId)> first,
            IList<(string MachineCode, string NodeId)> second)
        {
            if (first.Count != second.Count)
            {
                return false;
            }
            // Normalize the tuples to a common format: trim and uppercase the machine code
            var normalizedFirst = first.Select(t => (Machine: t.MachineCode.Trim().ToUpperInvariant(), Node: t.NodeId)).ToHashSet();
            var normalizedSecond = second.Select(t => (Machine: t.MachineCode.Trim().ToUpperInvariant(), Node: t.NodeId)).ToHashSet();
            return normalizedFirst.SetEquals(normalizedSecond);
        }

        /// <summary>
        /// Constructs a new Worker instance.  Dependencies are injected by the host via
        /// constructor injection.
        /// </summary>
        /// <param name="logger">Logger for logging messages.</param>
        /// <param name="scopeFactory">Factory for creating scoped service providers.</param>
        /// <param name="bus">Bus used to publish MachineTagValue entities.</param>
        /// <param name="loggerFactory">Factory for creating loggers for runtime components.</param>
        public Worker(
            ILogger<Worker> logger,
            IServiceScopeFactory scopeFactory,
            ITagValueBus bus,
            ILoggerFactory loggerFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _bus = bus;
            _loggerFactory = loggerFactory;
        }

        /// <summary>
        /// This method is called by the generic host to start the service.  It loads
        /// configuration data, creates and starts an OpcEndpointRunner for each endpoint,
        /// and waits for all runners to complete.  Cancellation is propagated via
        /// the provided token.
        /// </summary>
        /// <param name="stoppingToken">Token that signals when the host is shutting down.</param>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Log that the worker has started and will perform dynamic reloads.
            _logger.LogInformation("OPC runtime starting with dynamic configuration reload");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Resolve scoped services for this iteration
                    using var reloadScope = _scopeFactory.CreateScope();
                    var loader = reloadScope.ServiceProvider.GetRequiredService<ConfigLoader>();
                    var sessionManager = reloadScope.ServiceProvider.GetRequiredService<OpcSessionManager>();
                    var subscriptionManager = reloadScope.ServiceProvider.GetRequiredService<OpcSubscriptionManager>();

                    // Fetch current configuration
                    var endpointList = (await loader.LoadEndpointsAsync()).ToList();
                    var machineList = (await loader.LoadMachinesAsync()).ToList();
                    var tagList = (await loader.LoadTagMappingsAsync()).ToList();

                    // Build configuration map: endpoint id -> (endpoint, nodes)
                    var configMap = new Dictionary<int, (Mes.Opc.Acquisition.Runtime.Configuration.OpcConfigModels.OpcEndpointConfig Endpoint, List<(string MachineCode, string NodeId)> Nodes)>();
                    foreach (var ep in endpointList)
                    {
                        var epMachines = machineList.Where(m => m.OpcEndpointId == ep.Id).ToList();
                        var nodeList = new List<(string MachineCode, string NodeId)>();
                        foreach (var machine in epMachines)
                        {
                            var machineTags = tagList.Where(t => t.MachineCode.Trim().Equals(machine.MachineCode.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
                            foreach (var tag in machineTags)
                            {
                                nodeList.Add((machine.MachineCode, tag.OpcNodeId));
                            }
                        }
                        if (nodeList.Any())
                        {
                            configMap[ep.Id] = (ep, nodeList);
                        }
                    }

                    // Start or restart runners for endpoints in config
                    foreach (var entry in configMap)
                    {
                        var id = entry.Key;
                        var ep = entry.Value.Endpoint;
                        var nodeList = entry.Value.Nodes;
                        if (_runners.TryGetValue(id, out var existing))
                        {
                            bool endpointChanged = !string.Equals(existing.Endpoint.EndpointUrl, ep.EndpointUrl, StringComparison.OrdinalIgnoreCase) || !string.Equals(existing.Endpoint.Name, ep.Name, StringComparison.Ordinal);
                            bool nodesChanged = !NodeSetsEqual(existing.Nodes, nodeList);
                            if (endpointChanged || nodesChanged)
                            {
                                _logger.LogInformation("OPC | Restarting runner for Endpoint={endpointName}", ep.Name);
                                existing.Cancellation.Cancel();
                                try { await existing.Task.ConfigureAwait(false); } catch { }
                                _runners.Remove(id);
                            }
                            else
                            {
                                // Runner up to date; skip
                                continue;
                            }
                        }
                        // Start new runner
                        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                        var runnerLogger = _loggerFactory.CreateLogger<Implementation.OpcEndpointRunner>();
                        var runner = new Implementation.OpcEndpointRunner(ep, nodeList, sessionManager, subscriptionManager, runnerLogger, _bus);
                        var runnerTask = runner.RunAsync(linkedCts.Token);
                        _runners[id] = new RunnerInfo { Endpoint = ep, Nodes = nodeList, Runner = runner, Cancellation = linkedCts, Task = runnerTask };
                        _logger.LogInformation("OPC | Started runner for Endpoint={endpointName} with {nodeCount} nodes", ep.Name, nodeList.Count);
                    }

                    // Stop runners for endpoints removed from configuration
                    var removedIds = _runners.Keys.Except(configMap.Keys).ToList();
                    foreach (var removeId in removedIds)
                    {
                        var info = _runners[removeId];
                        _logger.LogInformation("OPC | Stopping runner for Endpoint={endpointName} (removed from config)", info.Endpoint.Name);
                        info.Cancellation.Cancel();
                        try { await info.Task.ConfigureAwait(false); } catch { }
                        _runners.Remove(removeId);
                    }
                }
                catch (Exception reloadEx)
                {
                    _logger.LogError(reloadEx, "OPC | Error during configuration reload");
                }

                // Wait for reload interval or until cancellation
                try { await Task.Delay(_reloadInterval, stoppingToken); } catch (TaskCanceledException) { break; }
            }

            // Cancel and await running runners on shutdown
            foreach (var kv in _runners)
            {
                kv.Value.Cancellation.Cancel();
            }
            await Task.WhenAll(_runners.Values.Select(v => v.Task));
        }
    }
}