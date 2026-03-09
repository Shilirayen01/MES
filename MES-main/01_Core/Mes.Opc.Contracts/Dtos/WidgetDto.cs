using System;
using System.Collections.Generic;

namespace Mes.Opc.Contracts.Dtos
{
    /// <summary>
    /// Data transfer object representing a widget.  Contains its type as a
    /// string and any properties or bindings required by the front‑end.
    /// </summary>
    public sealed class WidgetDto
    {
        /// <summary>
        /// Gets or sets the unique identifier of the widget.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the zone containing this widget.
        /// </summary>
        public Guid ZoneId { get; set; }

        /// <summary>
        /// Gets or sets the title displayed for the widget.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type of widget as a string (e.g., "KpiCard").  Using
        /// a string here decouples the API from the domain enum definitions.
        /// </summary>
        public string WidgetType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a JSON string containing widget properties (unit,
        /// decimals, thresholds, etc.).
        /// </summary>
        public string? PropsJson { get; set; }

        /// <summary>
        /// Contains the collection of bindings for this widget.
        /// </summary>
        public List<WidgetBindingDto> Bindings { get; set; } = new();
    }
}