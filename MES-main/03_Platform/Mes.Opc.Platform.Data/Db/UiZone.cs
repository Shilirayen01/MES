using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Mes.Opc.Platform.Data.Db;

[Table("UiZone")]
public partial class UiZone
{
    [Key]
    public Guid Id { get; set; }

    public Guid DashboardId { get; set; }

    [StringLength(200)]
    public string Title { get; set; } = null!;

    [StringLength(50)]
    public string LayoutType { get; set; } = null!;

    public string? PropsJson { get; set; }

    public int OrderIndex { get; set; }

    [Precision(3)]
    public DateTime CreatedAtUtc { get; set; }

    [Precision(3)]
    public DateTime? UpdatedAtUtc { get; set; }

    [ForeignKey("DashboardId")]
    [InverseProperty("UiZones")]
    public virtual UiDashboard Dashboard { get; set; } = null!;

    [InverseProperty("Zone")]
    public virtual ICollection<UiWidget> UiWidgets { get; set; } = new List<UiWidget>();
}
