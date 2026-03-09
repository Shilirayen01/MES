using System;
using Mes.Opc.Domain.Enums;

namespace Mes.Opc.Domain.Models
{
    /// <summary>
    /// Defines the relationship between a widget and a specific OPC UA tag.
    /// A widget can have multiple bindings, each serving a different
    /// functional role (value, status, setpoint, alarm).
    /// </summary>
    public sealed class UiWidgetBinding
    {
        /// <summary>
        /// Gets or sets the unique identifier of the binding.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the parent widget.
        /// </summary>
        public Guid WidgetId { get; set; }

        /// <summary>
        /// Gets or sets the machine code associated with the OPC UA tag.  This
        /// should correspond to a row in the Machine table of the configuration.
        /// </summary>
        public string MachineCode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the OPC UA node identifier.  The node ID uniquely identifies
        /// the variable or data point on the server.
        /// </summary>
        public string OpcNodeId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the functional role that this binding plays within the widget.
        /// </summary>
        public BindingRole BindingRole { get; set; } = BindingRole.Value;

        /// <summary>
        /// UTC timestamp when the binding was created.
        /// </summary>
        public DateTime CreatedAtUtc { get; set; }
    }
}