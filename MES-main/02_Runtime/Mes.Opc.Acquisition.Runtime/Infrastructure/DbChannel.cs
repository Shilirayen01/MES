using System.Threading.Channels;
using Mes.Opc.Acquisition.Runtime.Persistence;

namespace Mes.Opc.Acquisition.Runtime.Infrastructure;

/// <summary>
/// Wrapper DI around a <see cref="Channel{T}"/> destined for database persistence.
/// The channel uses the Wait overflow policy so that producers will wait
/// rather than drop values when the buffer is full.  This ensures no
/// historical data is lost.
/// </summary>
public sealed class DbChannel
{
    /// <summary>
    /// Gets the underlying channel used for persisting <see cref="MachineTagValue"/> entities.
    /// </summary>
    public Channel<MachineTagValue> Channel { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DbChannel"/> class.
    /// </summary>
    /// <param name="channel">The channel to wrap.</param>
    public DbChannel(Channel<MachineTagValue> channel)
    {
        Channel = channel;
    }
}