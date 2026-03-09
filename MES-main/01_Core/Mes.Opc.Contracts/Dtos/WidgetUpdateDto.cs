using System;

namespace Mes.Opc.Contracts.Dtos
{
    /// <summary>
    /// Represents a realtime UI update targeted at a specific widget.
    ///
    /// The goal is to keep the UI independent from OPC concepts
    /// (MachineCode, NodeId). The backend resolves OPC tag updates to the
    /// widgets that depend on them (via UiWidgetBinding) and pushes these
    /// messages over SignalR.
    /// </summary>
    public sealed class WidgetUpdateDto
    {
        /// <summary>The widget to update.</summary>
        public Guid WidgetId { get; init; }

        /// <summary>
        /// The binding role for the widget (e.g. "Value", "Status", "Counter").
        /// This allows a single widget to receive multiple values.
        /// </summary>
        public string BindingRole { get; init; } = "Value";

        /// <summary>The value to display (already formatted as text).</summary>
        public string? Value { get; init; }

        /// <summary>UTC timestamp when the backend produced the update.</summary>
        public DateTime TimestampUtc { get; init; }
    }
}
