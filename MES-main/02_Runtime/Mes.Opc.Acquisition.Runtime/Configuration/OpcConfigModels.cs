// -------------------------------------------------------------------------------------------------
// OpcConfigModels.cs
//
// Defines simple record types used to represent configuration data loaded
// from the database.  Records are immutable, value‑based types that simplify
// comparisons and usage.  Each record corresponds to a table in the
// configuration database.  Comments on each record and parameter describe
// their purpose.
// -------------------------------------------------------------------------------------------------

namespace Mes.Opc.Acquisition.Runtime.Configuration
{
    /// <summary>
    /// Container class for configuration record types.  The outer class
    /// exists only to logically group the records; it is not intended to be
    /// instantiated.
    /// </summary>
    public static class OpcConfigModels
    {
        /// <summary>
        /// Represents a single OPC endpoint configuration.  Each endpoint
        /// defines a unique identifier, a human‑readable name and the
        /// network URL of the OPC UA server.
        /// </summary>
        /// <param name="Id">The primary key of the endpoint in the database.</param>
        /// <param name="Name">A descriptive name for the endpoint (e.g., plant location).</param>
        /// <param name="EndpointUrl">The OPC UA discovery or endpoint URL.</param>
        public record OpcEndpointConfig(
            int Id,
            string Name,
            string EndpointUrl
        );

        /// <summary>
        /// Represents a machine configuration.  A machine is associated with
        /// exactly one OPC endpoint via the <see cref="OpcEndpointId"/>,
        /// indicating to which server it belongs.
        /// </summary>
        /// <param name="MachineCode">A unique code identifying the machine (e.g., "M01").</param>
        /// <param name="OpcEndpointId">The Id of the OPC endpoint to which this machine connects.</param>
        public record MachineConfig(
            string MachineCode,
            int OpcEndpointId
        );

        /// <summary>
        /// Represents a mapping between a machine and a specific OPC UA node.
        /// Each mapping ties a machine code to a NodeId, allowing the system
        /// to know which node on the server corresponds to which machine.
        /// </summary>
        /// <param name="MachineCode">The machine code that this mapping belongs to.</param>
        /// <param name="OpcNodeId">The fully qualified OPC UA NodeId string.</param>
        public record TagMappingConfig(
            string MachineCode,
            string OpcNodeId
        );
    }
}