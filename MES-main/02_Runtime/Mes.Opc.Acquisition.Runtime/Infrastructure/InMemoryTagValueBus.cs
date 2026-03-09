// -------------------------------------------------------------------------------------------------
// InMemoryTagValueBus.cs
//
// Provides an implementation of the ITagValueBus interface that fans out each
// MachineTagValue to two underlying channels: one intended for database
// persistence and the other for real‑time streaming (e.g., SignalR).  Each
// channel can be configured with its own bounded capacity and overflow
// policy via the DI container.  The bus reports whether a value was
// successfully enqueued on all channels so producers can monitor drops.
// -------------------------------------------------------------------------------------------------

using System.Threading.Channels;
using Mes.Opc.Acquisition.Runtime.Persistence;
using Microsoft.Extensions.Logging;

namespace Mes.Opc.Acquisition.Runtime.Infrastructure
{
    /// <summary>
    /// Fan‑out bus that publishes <see cref="MachineTagValue"/> instances to two
    /// separate channels.  One channel is intended for persistence to a
    /// database and the other for real‑time consumption (e.g., SignalR).  The
    /// bus applies the overflow policies of each channel independently.  If
    /// either channel rejects the value due to backpressure then the publish
    /// operation returns false.
    /// </summary>
    public sealed class InMemoryTagValueBus : ITagValueBus
    {
        private readonly Channel<MachineTagValue> _dbChannel;
        private readonly Channel<MachineTagValue> _rtChannel;
        private readonly ILogger<InMemoryTagValueBus> _logger;

        /// <summary>
        /// Creates a new instance of <see cref="InMemoryTagValueBus"/> with the specified
        /// channels and logger.  Both channels should be bounded and configured
        /// to enforce the desired backpressure behaviour (wait or drop).
        /// </summary>
        /// <param name="dbChannel">Channel used for database persistence.</param>
        /// <param name="rtChannel">Channel used for real‑time streaming.</param>
        /// <param name="logger">Logger for diagnostic messages.</param>
        public InMemoryTagValueBus(
            DbChannel dbChannel,
            RtChannel rtChannel,
            ILogger<InMemoryTagValueBus> logger)
        {
            _dbChannel = dbChannel.Channel;
            _rtChannel = rtChannel.Channel;
            _logger = logger;
        }

        /// <inheritdoc />
        public bool Publish(MachineTagValue value)
        {
            // DB channel is configured with FullMode.Wait to guarantee persistence.
            // Using TryWrite() would silently drop values once the bounded buffer is full.
            // Because Publish() is synchronous, we block briefly here to respect backpressure.
            bool dbOk;
            try
            {
                _dbChannel.Writer.WriteAsync(value).AsTask().GetAwaiter().GetResult();
                dbOk = true;
            }
            catch
            {
                dbOk = false;
            }
            bool rtOk = _rtChannel.Writer.TryWrite(value);
            if (!dbOk || !rtOk)
            {
                _logger.LogWarning(
                    "BUS DROP | DB={dbStatus} RT={rtStatus} | {machine} {node}",
                    dbOk,
                    rtOk,
                    value.MachineCode,
                    value.OpcNodeId);
            }
            return dbOk && rtOk;
        }
    }
}