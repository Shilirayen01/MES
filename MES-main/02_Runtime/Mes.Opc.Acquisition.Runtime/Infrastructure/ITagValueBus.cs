// -------------------------------------------------------------------------------------------------
// ITagValueBus.cs
//
// Defines an abstraction for a tag value bus used to fan out MachineTagValue
// instances to multiple independent consumers.  The runtime service publishes
// all incoming values through this bus so that consumers (e.g., database
// writer, real‑time publisher) do not compete for messages.  A bus
// implementation hides the details of channel management from the rest of
// the application and centralises backpressure and drop policies.
// -------------------------------------------------------------------------------------------------

using Mes.Opc.Acquisition.Runtime.Persistence;

namespace Mes.Opc.Acquisition.Runtime.Infrastructure
{
    /// <summary>
    /// Defines the contract for a bus that delivers <see cref="MachineTagValue"/>
    /// items to downstream channels.  Producers should publish values via an
    /// implementation of this interface rather than writing directly to
    /// channels.  The bus implementation is responsible for pushing each
    /// value to all configured channels and handling any backpressure or
    /// overflow conditions.
    /// </summary>
    public interface ITagValueBus
    {
        /// <summary>
        /// Publishes a tag value to all configured consumers.  The bus should
        /// return true if the value was accepted by all channels and false
        /// if it was dropped by any channel due to backpressure.  Implementations
        /// should not throw if a channel is full; they should log and return
        /// false instead.
        /// </summary>
        /// <param name="value">The tag value to publish.</param>
        /// <returns>True if the value was enqueued on all channels, otherwise false.</returns>
        bool Publish(MachineTagValue value);
    }
}