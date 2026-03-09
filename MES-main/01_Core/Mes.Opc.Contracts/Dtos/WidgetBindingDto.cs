using System;

namespace Mes.Opc.Contracts.Dtos
{
    /// <summary>
    /// Data transfer object describing the link between a widget and an OPC tag.
    /// The binding role is represented as a string for easier serialisation.
    /// </summary>
    public sealed class WidgetBindingDto
    {
        /// <summary>
        /// Gets or sets the unique identifier of the binding.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the widget to which this binding belongs.
        /// </summary>
        public Guid WidgetId { get; set; }

        /// <summary>
        /// Gets or sets the machine code associated with the tag.
        /// </summary>
        public string MachineCode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the OPC UA node identifier of the tag.
        /// </summary>
        public string OpcNodeId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the binding role as a string (e.g., "Value", "Status").
        /// </summary>
        public string BindingRole { get; set; } = string.Empty;
    }
}