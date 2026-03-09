// -------------------------------------------------------------------------------------------------
// OpcSubscriptionManager.cs
//
// Creates subscriptions and monitored items. Adds an optional "initial snapshot"
// Read() to publish current values even if the server doesn't send notifications
// for constant tags. This keeps ProductRunTracker strict gates intact.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Opc.Ua;
using Opc.Ua.Client;

namespace Mes.Opc.Acquisition.Runtime.Opc
{
    public class OpcSubscriptionManager
    {
        private readonly OpcSubscriptionOptions _options;
        private readonly ILogger<OpcSubscriptionManager> _logger;

        public OpcSubscriptionManager(
            IOptions<OpcSubscriptionOptions> options,
            ILogger<OpcSubscriptionManager> logger)
        {
            _options = options.Value ?? new OpcSubscriptionOptions();
            _logger = logger;
        }

        public Subscription CreateSubscription(
            Session session,
            IEnumerable<(string MachineCode, string NodeId)> nodes,
            Action<string, string, DataValue> onValueChanged)
        {
            // Materialize once (we need it both for monitored items and snapshot).
            var nodeList = nodes as IList<(string MachineCode, string NodeId)> ?? nodes.ToList();

            var subscription = new Subscription(session.DefaultSubscription)
            {
                PublishingInterval = 1000
            };

            foreach (var (machineCode, nodeId) in nodeList)
            {
                var item = new MonitoredItem(subscription.DefaultItem)
                {
                    StartNodeId = OpcNodeIdHelper.ToNodeId(session, nodeId),
                    AttributeId = Attributes.Value,
                    SamplingInterval = 1000,
                    QueueSize = 10,
                    DiscardOldest = true
                };

                item.Notification += (monItem, args) =>
                {
                    foreach (var value in monItem.DequeueValues())
                    {
                        onValueChanged(machineCode, nodeId, value);
                    }
                };

                subscription.AddItem(item);
            }

            session.AddSubscription(subscription);

            // Create subscription on server
            subscription.Create();

            // ApplyChanges ensures monitored items are created on server
            subscription.ApplyChanges();

            // Optional: Initial snapshot read to seed "latest values" for constant tags.
            if (_options.InitialSnapshotEnabled)
            {
                try
                {
                    PublishInitialSnapshot(session, nodeList, onValueChanged);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "OPC | Initial snapshot failed");
                }
            }

            return subscription;
        }

        private void PublishInitialSnapshot(
            Session session,
            IList<(string MachineCode, string NodeId)> nodes,
            Action<string, string, DataValue> onValueChanged)
        {
            if (nodes.Count == 0) return;

            var chunkSize = _options.InitialSnapshotChunkSize <= 0 ? 200 : _options.InitialSnapshotChunkSize;
            var published = 0;

            _logger.LogInformation(
                "OPC | Initial snapshot start | Nodes={count} | ChunkSize={chunk}",
                nodes.Count,
                chunkSize);

            for (int offset = 0; offset < nodes.Count; offset += chunkSize)
            {
                var chunk = nodes.Skip(offset).Take(chunkSize).ToList();

                var readIds = new ReadValueIdCollection();
                foreach (var (_, nodeId) in chunk)
                {
                    // Parse node id as-is (your "=DB311,..." suffix is part of the string identifier).
                    readIds.Add(new ReadValueId
                    {
                        NodeId = OpcNodeIdHelper.ToNodeId(session, nodeId),
                        AttributeId = Attributes.Value
                    });
                }

                session.Read(
                    requestHeader: null,
                    maxAge: 0,
                    timestampsToReturn: TimestampsToReturn.Both,
                    nodesToRead: readIds,
                    results: out DataValueCollection results,
                    diagnosticInfos: out DiagnosticInfoCollection _);

                // results count should match chunk size
                var n = Math.Min(chunk.Count, results.Count);

                for (int i = 0; i < n; i++)
                {
                    var (machineCode, nodeId) = chunk[i];
                    var dv = results[i] ?? new DataValue(StatusCodes.BadNoData);

                    // If SourceTimestamp is missing, try to use ServerTimestamp; else keep as-is.
                    if (dv.SourceTimestamp == DateTime.MinValue && dv.ServerTimestamp != DateTime.MinValue)
                        dv.SourceTimestamp = dv.ServerTimestamp;

                    if (!_options.InitialSnapshotIncludeBadStatus && ServiceResult.IsBad(dv.StatusCode))
                        continue;

                    onValueChanged(machineCode, nodeId, dv);
                    published++;
                }
            }

            _logger.LogInformation(
                "OPC | Initial snapshot done | Published={published}",
                published);
        }
    }
}
