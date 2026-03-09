// -------------------------------------------------------------------------------------------------
// IOpcEndpointRunner.cs
//
// Defines the contract for a component that manages an OPC UA endpoint.  Implementations of
// this interface are responsible for connecting to an endpoint, subscribing to
// configured nodes and forwarding values to consumers.  Having an interface
// simplifies testing and allows multiple implementations if required.
// -------------------------------------------------------------------------------------------------

using System.Threading;      // Provides CancellationToken used for cooperative cancellation
using System.Threading.Tasks; // Provides Task for asynchronous method signatures

namespace Mes.Opc.Acquisition.Runtime.Interface
{
    /// <summary>
    /// Represents a long‑running operation that communicates with an OPC UA endpoint.
    /// The implementation should attempt to connect and reconnect as needed and exit
    /// when cancellation is requested.
    /// </summary>
    public interface IOpcEndpointRunner
    {
        /// <summary>
        /// Starts the runner.  Implementations should not throw exceptions; instead
        /// they should run until the cancellation token is signaled.
        /// </summary>
        /// <param name="stoppingToken">
        /// A token that is signaled when the host is shutting down.  The runner should
        /// observe this token and stop gracefully when it is canceled.
        /// </param>
        /// <returns>A task that represents the lifetime of the runner.</returns>
        Task RunAsync(CancellationToken stoppingToken);
    }
}