using System;
using System.Collections.Generic;

namespace Mes.Opc.Domain.Models
{
    /// <summary>
    /// Represents a logical section of a MES dashboard.  A dashboard is
    /// composed of one or more zones, each grouping related widgets.  Zones
    /// control the layout of their widgets (grid, rows, columns) and can
    /// include additional layout properties encoded as JSON.
    /// </summary>
    public sealed class UiZone
    {
        /// <summary>
        /// Gets or sets the unique identifier of the zone.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the parent dashboard.  This
        /// establishes the relationship between a zone and its dashboard.
        /// </summary>
        public Guid DashboardId { get; set; }

        /// <summary>
        /// Gets or sets the display title of the zone (e.g., "Production").
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Defines the order in which zones appear within a dashboard.  Lower
        /// numbers appear earlier.  Use this to control the vertical
        /// positioning of zones on the page.
        /// </summary>
        public int OrderIndex { get; set; }

        /// <summary>
        /// Gets or sets the layout type used to arrange widgets in the zone.
        /// Typical values are "Grid", "Rows" or "Columns".  The front end
        /// interprets this value to choose an appropriate CSS layout.
        /// </summary>
        public string LayoutType { get; set; } = "Grid";

        /// <summary>
        /// JSON‑encoded string containing additional layout properties such as
        /// number of columns, fixed heights or responsive breakpoints.  The
        /// interpretation of this JSON is the responsibility of the front end.
        /// </summary>
        public string? PropsJson { get; set; }

        /// <summary>
        /// UTC timestamp when the zone was created.
        /// </summary>
        public DateTime CreatedAtUtc { get; set; }

        /// <summary>
        /// UTC timestamp when the zone was last updated.
        /// </summary>
        public DateTime? UpdatedAtUtc { get; set; }

        /// <summary>
        /// Navigation property containing the collection of widgets that
        /// belong to this zone.  Widgets are ordered by their OrderIndex.
        /// </summary>
        public List<UiWidget> Widgets { get; set; } = new();
    }
}