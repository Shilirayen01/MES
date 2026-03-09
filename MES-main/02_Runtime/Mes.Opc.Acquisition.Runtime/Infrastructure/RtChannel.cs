using System.Threading.Channels;
using Mes.Opc.Acquisition.Runtime.Persistence;

namespace Mes.Opc.Acquisition.Runtime.Infrastructure;

/// <summary>
/// Wrapper DI around a <see cref="Channel{T}"/> destined for real‑time streaming.
/// The channel uses the DropOldest overflow policy so that new values never
/// block the acquisition system.  This ensures that the UI receives the
/// latest values even under load, at the cost of losing intermediate values.
/// </summary>
public sealed class RtChannel
{
    /// <summary>
    /// Gets the underlying channel used for real‑time streaming of
    /// <see cref="MachineTagValue"/> entities.
    /// </summary>
    public Channel<MachineTagValue> Channel { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RtChannel"/> class.
    /// </summary>
    /// <param name="channel">The channel to wrap.</param>
    public RtChannel(Channel<MachineTagValue> channel)
    {
        Channel = channel;
    }
}