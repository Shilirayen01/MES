using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Mes.Opc.Platform.Data.Db;

[Table("UiWidget")]
public partial class UiWidget
{
    [Key]
    public Guid Id { get; set; }

    public Guid ZoneId { get; set; }

    [StringLength(200)]
    public string Title { get; set; } = null!;

    [StringLength(50)]
    public string WidgetType { get; set; } = null!;

    public string? PropsJson { get; set; }

    public int OrderIndex { get; set; }

    [Precision(3)]
    public DateTime CreatedAtUtc { get; set; }

    [Precision(3)]
    public DateTime? UpdatedAtUtc { get; set; }

    [InverseProperty("Widget")]
    public virtual ICollection<UiWidgetBinding> UiWidgetBindings { get; set; } = new List<UiWidgetBinding>();

    [ForeignKey("ZoneId")]
    [InverseProperty("UiWidgets")]
    public virtual UiZone Zone { get; set; } = null!;
}
