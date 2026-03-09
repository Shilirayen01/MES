using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Mes.Opc.Platform.Data.Db;

[Table("UiWidgetBinding")]
public partial class UiWidgetBinding
{
    [Key]
    public Guid Id { get; set; }

    public Guid WidgetId { get; set; }

    [StringLength(50)]
    public string MachineCode { get; set; } = null!;

    [StringLength(400)]
    public string OpcNodeId { get; set; } = null!;

    [StringLength(50)]
    public string BindingRole { get; set; } = null!;

    [ForeignKey("WidgetId")]
    [InverseProperty("UiWidgetBindings")]
    public virtual UiWidget Widget { get; set; } = null!;
}
