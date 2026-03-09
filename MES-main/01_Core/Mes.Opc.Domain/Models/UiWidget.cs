using System;
using System.Collections.Generic;
using Mes.Opc.Domain.Enums;

namespace Mes.Opc.Domain.Models
{
    /// <summary>
    /// Represents a user‑interface component displayed within a zone.  Each
    /// widget can bind to one or more OPC UA tags via bindings.
    /// </summary>
    public sealed class UiWidget
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
        /// Gets or sets the display title of the widget (e.g., "Vitesse").
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type of the widget (e.g., KPI card, gauge, trend).
        /// </summary>
        public WidgetType WidgetType { get; set; } = WidgetType.KpiCard;

        /// <summary>
        /// Defines the order in which widgets appear within a zone.  Lower
        /// numbers appear first.
        /// </summary>
        public int OrderIndex { get; set; }

        /// <summary>
        /// JSON‑encoded string containing additional widget properties such as
        /// unit, number of decimals, thresholds, min and max values.  The front
        /// end interprets this JSON according to the widget type.
        /// </summary>
        public string? PropsJson { get; set; }

        /// <summary>
        /// UTC timestamp when the widget was created.
        /// </summary>
        public DateTime CreatedAtUtc { get; set; }

        /// <summary>
        /// UTC timestamp when the widget was last updated.
        /// </summary>
        public DateTime? UpdatedAtUtc { get; set; }

        /// <summary>
        /// Navigation property containing the bindings from this widget to OPC tags.
        /// </summary>
        public List<UiWidgetBinding> Bindings { get; set; } = new();
    }
}