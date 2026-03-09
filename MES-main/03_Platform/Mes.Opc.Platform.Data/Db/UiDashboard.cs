using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Mes.Opc.Platform.Data.Db;

[Table("UiDashboard")]
public partial class UiDashboard
{
    [Key]
    public Guid Id { get; set; }

    [StringLength(200)]
    public string Name { get; set; } = null!;

    [StringLength(500)]
    public string? Description { get; set; }

    public bool IsDefault { get; set; }

    [Precision(3)]
    public DateTime CreatedAtUtc { get; set; }

    [Precision(3)]
    public DateTime? UpdatedAtUtc { get; set; }

    [InverseProperty("Dashboard")]
    public virtual ICollection<UiZone> UiZones { get; set; } = new List<UiZone>();
}
