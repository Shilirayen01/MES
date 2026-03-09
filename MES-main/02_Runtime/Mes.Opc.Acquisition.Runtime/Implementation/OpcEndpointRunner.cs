// -------------------------------------------------------------------------------------------------
// OpcEndpointRunner.cs
//
// An OpcEndpointRunner manages the lifecycle of a single connection to an OPC UA
// endpoint.  It establishes an OPC session, subscribes to a set of nodes,
// monitors keep‑alive notifications, reconnects on failure and forwards all
// received values to a tag value bus.  The bus fans out values to downstream
// channels (for persistence and real‑time delivery).  Each line is commented
// so that the behaviour of the runner is completely transparent.
// -------------------------------------------------------------------------------------------------

using System;                                         // Provides basic .NET types
using System.Collections.Generic;                     // Provides generic collections such as IEnumerable<T>
using System.Linq;                                    // Provides LINQ operators
using System.Security.Cryptography;                    // Provides hashing algorithms
using System.Text;                                    // Provides string encoding
using System.Threading;                               // Provides CancellationToken and synchronization primitives
using System.Threading.Tasks;                          // Provides asynchronous programming support
using Mes.Opc.Acquisition.Runtime.Infrastructure;     // Imports the tag value bus interface
using Mes.Opc.Acquisition.Runtime.Interface;          // Imports the IOpcEndpointRunner interface definition
using Mes.Opc.Acquisition.Runtime.Opc;                // Imports session and subscription managers
using Mes.Opc.Acquisition.Runtime.Persistence;        // Imports the MachineTagValue data model
using Microsoft.Extensions.Logging;                  // Provides logging abstractions
using Opc.Ua;                                        // Imports OPC UA base types
using Opc.Ua.Client;                                  // Imports OPC UA client types such as Session and Subscription
using static Mes.Opc.Acquisition.Runtime.Configuration.OpcConfigModels; // Imports record types for endpoint and machine/tag configuration

namespace Mes.Opc.Acquisition.Runtime.Implementation
{
    /// <summary>
    /// Represents a runner responsible for a single OPC UA endpoint.  The runner
    /// connects to the endpoint, subscribes to configured nodes and writes
    /// incoming values into an <see cref="ITagValueBus"/> so they can be
    /// persisted by another component or delivered in real time.  If the
    /// connection drops or a keep‑alive failure is detected, the runner
    /// cleans up the session and attempts to reconnect after a delay.
    /// </summary>
    public sealed class OpcEndpointRunner : IOpcEndpointRunner
    {
        private readonly OpcEndpointConfig _endpoint;                  // The configuration describing the endpoint (name, URL, ID)
        private readonly IEnumerable<(string MachineCode, string NodeId)> _nodes; // The set of nodes to subscribe to, paired with their machine codes
        private readonly OpcSessionManager _sessionManager;             // Service used to create OPC UA sessions
        private readonly OpcSubscriptionManager _subscriptionManager;   // Service used to create OPC UA subscriptions
        private readonly ILogger<OpcEndpointRunner> _logger;           // Logger instance for reporting events and errors
        private readonly ITagValueBus _bus;                            // Bus used to publish values to downstream channels

        private Session? _session;                                    // The current OPC UA session; null when disconnected

        // Signature of the server NamespaceTable (Session.NamespaceUris) from the last
        // successful connection. If it changes, we log it explicitly.
        private string? _lastNamespaceSignature;

        /// <summary>
        /// Constructs a new <see cref="OpcEndpointRunner"/>.
        /// </summary>
        /// <param name="endpoint">The endpoint configuration describing where to connect.</param>
        /// <param name="nodes">The collection of (MachineCode, NodeId) pairs to subscribe to.</param>
        /// <param name="sessionManager">The session manager used to create OPC UA sessions.</param>
        /// <param name="subscriptionManager">The subscription manager used to create subscriptions.</param>
        /// <param name="logger">A logger for this runner instance.</param>
        /// <param name="bus">The bus used to publish values to downstream channels.</param>
        public OpcEndpointRunner(
            OpcEndpointConfig endpoint,
            IEnumerable<(string MachineCode, string NodeId)> nodes,
            OpcSessionManager sessionManager,
            OpcSubscriptionManager subscriptionManager,
            ILogger<OpcEndpointRunner> logger,
            ITagValueBus bus)
        {
            _endpoint = endpoint;
            _nodes = nodes;
            _sessionManager = sessionManager;
            _subscriptionManager = subscriptionManager;
            _logger = logger;
            _bus = bus;
        }

        /// <summary>
        /// Starts the runner.  This method loops until cancellation is requested.  Within the loop
        /// it attempts to connect to the OPC endpoint, creates a subscription for all nodes,
        /// monitors the session and reconnects on failure.  On each iteration of the loop,
        /// cleanup is performed before waiting for a short delay and retrying.
        /// </summary>
        /// <param name="stoppingToken">Token that signals when the runner should stop.</param>
        /// <returns>A task that completes when the runner has stopped.</returns>
        public async Task RunAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation(
                        "OPC | Connecting to {endpointUrl}",
                        _endpoint.EndpointUrl);
                    _session = await _sessionManager.CreateSessionAsync(
                        _endpoint.EndpointUrl);

                    DetectAndLogNamespaceChanges(_session);
                    _session.KeepAlive += OnKeepAlive;
                    _subscriptionManager.CreateSubscription(
                        _session,
                        _nodes,
                        OnValueChanged);
                    _logger.LogInformation(
                        "OPC | Connected {endpointName}",
                        _endpoint.Name);
                    await WaitUntilDisconnected(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "OPC | Connection failed {endpointUrl}",
                        _endpoint.EndpointUrl);
                }
                await CleanupAsync();
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Computes a signature of the server NamespaceTable and logs when it changes.
        /// This is useful for diagnosing issues when servers (like Softing dataFEED) change
        /// namespace indexes between restarts.
        /// </summary>
        private void DetectAndLogNamespaceChanges(Session session)
        {
            try
            {
                var signature = ComputeNamespaceSignature(session);
                if (_lastNamespaceSignature is null)
                {
                    _lastNamespaceSignature = signature;
                    _logger.LogInformation(
                        "OPC | NamespaceTable loaded | Count={count}",
                        session.NamespaceUris.Count);
                    return;
                }

                if (!string.Equals(_lastNamespaceSignature, signature, StringComparison.Ordinal))
                {
                    _logger.LogWarning(
                        "OPC | NamespaceTable CHANGED since last connection | Endpoint={endpointName} | OldSig={oldSig} | NewSig={newSig}",
                        _endpoint.Name,
                        _lastNamespaceSignature,
                        signature);

                    // Dump the table to help identify the correct NSU for a given ns=<index>
                    for (int i = 0; i < session.NamespaceUris.Count; i++)
                    {
                        _logger.LogInformation("OPC | NS[{index}] = {uri}", i, session.NamespaceUris.GetString((uint)i));
                    }

                    _lastNamespaceSignature = signature;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "OPC | Unable to compute NamespaceTable signature");
            }
        }

        private static string ComputeNamespaceSignature(Session session)
        {
            // Build a stable text representation: one line per index.
            var sb = new StringBuilder();
            for (int i = 0; i < session.NamespaceUris.Count; i++)
            {
                sb.Append(i);
                sb.Append('=');
                sb.Append(session.NamespaceUris.GetString((uint)i));
                sb.Append('\n');
            }

            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var hash = sha.ComputeHash(bytes);
            // short hex (first 12 bytes) is enough for logs
            return string.Concat(hash.Take(12).Select(b => b.ToString("x2")));
        }

        /// <summary>
        /// Handles keep‑alive events emitted by the OPC UA client SDK.  If the keep‑alive
        /// status is bad, the session is closed in order to trigger a reconnection.
        /// </summary>
        /// <param name="sender">The session that emitted the keep‑alive event.</param>
        /// <param name="e">Information about the keep‑alive event, including status.</param>
        private void OnKeepAlive(object sender, KeepAliveEventArgs e)
        {
            var session = sender as Session;
            if (session == null)
            {
                return;
            }
            if (e.Status != null && ServiceResult.IsBad(e.Status))
            {
                _logger.LogWarning(
                    "OPC | KeepAlive BAD | Forcing reconnect | Endpoint={endpointName} | Status={status}",
                    _endpoint.Name,
                    e.Status);
                try
                {
                    session.Close();
                }
                catch
                {
                    // Suppress any exceptions during closure; cleanup will handle them
                }
            }
        }

        /// <summary>
        /// Waits until the current session is disconnected or the cancellation token is signaled.
        /// </summary>
        /// <param name="token">A token that can signal cancellation.</param>
        private async Task WaitUntilDisconnected(CancellationToken token)
        {
            while (_session != null && _session.Connected && !token.IsCancellationRequested)
            {
                await Task.Delay(1000, token);
            }
        }

        /// <summary>
        /// Cleans up the session by closing and disposing it if it exists.  Errors during
        /// cleanup are suppressed because the runner always attempts to reconnect.
        /// </summary>
        private async Task CleanupAsync()
        {
            try
            {
                if (_session != null)
                {
                    _logger.LogInformation(
                        "OPC | Closing session {endpointName}",
                        _endpoint.Name);
                    _session.Close();
                    _session.Dispose();
                    _session = null;
                }
            }
            catch
            {
                // Ignore any exceptions during cleanup; reconnection will be attempted anyway
            }
            await Task.CompletedTask;
        }

        /// <summary>
        /// Callback invoked whenever a monitored item notifies that a value has changed.
        /// This method converts the data into a MachineTagValue and attempts to
        /// publish it on the bus.  If any channel rejects the value due to
        /// backpressure the publish operation returns false and a warning is logged.
        /// </summary>
        /// <param name="machine">The machine code associated with the tag.</param>
        /// <param name="node">The OPC node identifier.</param>
        /// <param name="value">The data value received from the OPC server.</param>
        private void OnValueChanged(
            string machine,
            string node,
            DataValue value)
        {
            var entity = new MachineTagValue
            {
                MachineCode = machine,
                OpcNodeId = node,
                Value = value.Value?.ToString(),
                StatusCode = value.StatusCode.ToString(),
                SourceTimestamp = value.SourceTimestamp == DateTime.MinValue ? null : value.SourceTimestamp
            };
            bool accepted = _bus.Publish(entity);
            if (!accepted)
            {
                _logger.LogWarning(
                    "OPC | Bus drop, one or more channels full | Machine={machineCode} Node={nodeId}",
                    machine,
                    node);
            }
        }

        /// <summary>
        /// Computes a stable signature (hash) of the server NamespaceTable (Session.NamespaceUris).
        /// If it changes across reconnects, we log a warning and dump the new table.
        ///
        /// Why:
        /// - When the server restarts (or configuration changes), Namespace indices can shift.
        /// - With NSU-based NodeIds, the runtime stays stable, but detecting this change is
        ///   valuable for monitoring and incident analysis.
        /// </summary>
       

        
        private void DumpNamespaceTable(Session session)
        {
            // Emit the full table as individual lines (easier to grep).
            for (int i = 0; i < session.NamespaceUris.Count; i++)
            {
                _logger.LogWarning("OPC | NS[{index}] = {uri}", i, session.NamespaceUris.GetString((uint)i));
            }
        }
    }
}