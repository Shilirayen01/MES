using System;
using System.Collections.Generic;

namespace Mes.Opc.Domain.Models
{
    /// <summary>
    /// Represents a configurable MES dashboard.  A dashboard groups multiple
    /// zones which themselves contain widgets.  Instances of this class are
    /// persisted in the database to allow dynamic UI composition.
    /// </summary>
    public sealed class UiDashboard
    {
        /// <summary>
        /// Gets or sets the unique identifier of the dashboard.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the display name of the dashboard (e.g., "Ligne 3 – Production").
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets an optional description to explain the purpose or context of the dashboard.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Indicates whether the dashboard is active and should be presented to users.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Gets or sets the UTC timestamp when the dashboard was created.
        /// </summary>
        public DateTime CreatedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when the dashboard was last updated.
        /// </summary>
        public DateTime? UpdatedAtUtc { get; set; }

        /// <summary>
        /// Navigation property containing the collection of zones that belong to this dashboard.
        /// </summary>
        public List<UiZone> Zones { get; set; } = new();
    }
}