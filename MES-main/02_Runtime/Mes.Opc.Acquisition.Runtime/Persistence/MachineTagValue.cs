// -------------------------------------------------------------------------------------------------
// MachineTagValue.cs
//
// Defines a simple data transfer object representing a value read from an OPC UA
// server associated with a particular machine and node.  Each property is
// annotated to indicate its purpose.  Instances of this class are
// published by endpoint runners and consumed by the writer service to
// persist batches of values into the database.
// -------------------------------------------------------------------------------------------------

using System; // Provides DateTime for the SourceTimestamp property

namespace Mes.Opc.Acquisition.Runtime.Persistence
{
    /// <summary>
    /// Represents a single tag value read from the OPC UA server along with
    /// metadata identifying the machine, node and status.  This class is
    /// used to transfer data between the acquisition layer and the persistence
    /// layer via a channel.
    /// </summary>
    public class MachineTagValue
    {
        /// <summary>
        /// Optional production run identifier associated with this measurement.
        /// When cycle tracking (ProductRunTracker) is enabled, all values
        /// produced during a single product cycle share the same identifier.
        /// </summary>
        public Guid? ProductionRunId { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the machine that produced the value.  This
        /// value corresponds to the MachineCode column in the database.  The
        /// non‑nullable type indicates that a machine code is required.
        /// </summary>
        public string MachineCode { get; set; } = default!;

        /// <summary>
        /// Gets or sets the OPC UA node identifier associated with the value.  This
        /// should match the value stored in the MachineTagMapping table.  The
        /// node ID identifies the specific sensor or data point on the server.
        /// </summary>
        public string OpcNodeId { get; set; } = default!;

        /// <summary>
        /// Gets or sets the value read from the OPC UA server.  Because OPC nodes may
        /// return null values (for example, sensors that have not yet reported data),
        /// this property is nullable.  The value is stored as a string to allow
        /// flexible representation of numeric, boolean or textual data.
        /// </summary>
        public string? Value { get; set; }

        /// <summary>
        /// Gets or sets the status code returned by the OPC UA server.  Status
        /// codes convey the validity and quality of the data.  A non‑empty string
        /// is always expected.
        /// </summary>
        public string StatusCode { get; set; } = default!;

        /// <summary>
        /// Gets or sets the timestamp (in the source server's time) when the value
        /// was generated.  This is nullable because some OPC UA servers may
        /// return DateTime.MinValue to indicate that no timestamp is available.
        /// The writer treats DateTime.MinValue as null for consistency.
        /// </summary>
        public DateTime? SourceTimestamp { get; set; }
    }
}