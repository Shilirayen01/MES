namespace Mes.Opc.Contracts.Dtos
{
    /// <summary>
    /// Describes a machine in the MES catalogue.  In addition to its unique
    /// code, a machine may have a human‑readable name and an optional
    /// description for display purposes.
    /// </summary>
    public sealed class MachineDto
    {
        /// <summary>
        /// Gets or sets the unique code identifying the machine (e.g., "M01").
        /// </summary>
        public string MachineCode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets an optional human‑readable name for the machine.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets an optional description of the machine (e.g., location, function).
        /// </summary>
        public string? Description { get; set; }
    }
}