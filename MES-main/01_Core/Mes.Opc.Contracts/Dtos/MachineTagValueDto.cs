using System;

namespace Mes.Opc.Contracts.Dtos
{
    /// <summary>
    /// Represents the value of a tag returned from the OPC UA server.  This
    /// DTO mirrors the persistence model but is used for serialisation and API
    /// responses to clients.  All values are represented as strings for
    /// flexibility.
    /// </summary>
    public sealed class MachineTagValueDto
    {
        /// <summary>
        /// Gets or sets the machine code associated with the tag.
        /// </summary>
        public string MachineCode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the OPC UA node identifier of the tag.
        /// </summary>
        public string OpcNodeId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the value read from the server.  Represented as a string for
        /// flexibility across data types.
        /// </summary>
        public string? Value { get; set; }

        /// <summary>
        /// Gets or sets the OPC UA status code associated with the value.
        /// </summary>
        public string StatusCode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the timestamp on the source server when the value was generated.
        /// </summary>
        public DateTime? SourceTimestamp { get; set; }
    }
}