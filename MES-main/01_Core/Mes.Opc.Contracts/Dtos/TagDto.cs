namespace Mes.Opc.Contracts.Dtos
{
    /// <summary>
    /// Describes an OPC UA tag associated with a machine.  A tag identifies a
    /// variable on the server and may include metadata such as name,
    /// description and data type to assist the UI in presenting it.
    /// </summary>
    public sealed class TagDto
    {
        /// <summary>
        /// Gets or sets the machine code to which the tag belongs.
        /// </summary>
        public string MachineCode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the OPC UA node identifier of the tag.
        /// </summary>
        public string OpcNodeId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a human‑readable name for the tag.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets an optional description of the tag.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the OPC UA built‑in data type of the tag (e.g., "Int32", "Boolean").
        /// </summary>
        public string? DataType { get; set; }
    }
}