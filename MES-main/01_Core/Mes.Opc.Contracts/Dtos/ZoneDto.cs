using System;
using System.Collections.Generic;

namespace Mes.Opc.Contracts.Dtos
{
    /// <summary>
    /// Data transfer object representing a zone on a dashboard.  Includes
    /// nested widgets and layout information.
    /// </summary>
    public sealed class ZoneDto
    {
        /// <summary>
        /// Gets or sets the unique identifier of the zone.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the dashboard containing this zone.
        /// </summary>
        public Guid DashboardId { get; set; }

        /// <summary>
        /// Gets or sets the title displayed for the zone.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the layout type used to arrange widgets within the zone.
        /// </summary>
        public string LayoutType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a JSON string containing additional layout properties.
        /// </summary>
        public string? PropsJson { get; set; }

        /// <summary>
        /// Contains the collection of widgets within this zone.
        /// </summary>
        public List<WidgetDto> Widgets { get; set; } = new();
    }
}