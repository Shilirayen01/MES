using System;
using System.Collections.Generic;

namespace Mes.Opc.Contracts.Dtos
{
    /// <summary>
    /// Represents a dashboard returned by the API.  Contains its zones and
    /// nested widgets.  The DTO is designed to be consumed by the front‑end
    /// without exposing internal domain models.
    /// </summary>
    public sealed class DashboardDto
    {
        /// <summary>
        /// Gets or sets the unique identifier of the dashboard.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the name of the dashboard.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets an optional description for the dashboard.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Contains all zones belonging to this dashboard.
        /// </summary>
        public List<ZoneDto> Zones { get; set; } = new();
    }
}