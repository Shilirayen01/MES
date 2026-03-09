using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
namespace Mes.Opc.Platform.Data.Db;

[Table("MachineTagMapping")]
public partial class MachineTagMapping
{
    [Key]
    public int Id { get; set; }

    [StringLength(50)]
    public string MachineCode { get; set; } = null!;

    [StringLength(200)]
    public string OpcNodeId { get; set; } = null!;

    [StringLength(100)]
    public string CharacteristicCode { get; set; } = null!;

    [StringLength(50)]
    public string DataTypeExpected { get; set; } = null!;

    [StringLength(20)]
    public string? Unit { get; set; }

    public double? MinValue { get; set; }

    public double? MaxValue { get; set; }

    public int? SamplingMs { get; set; }

    public int? PublishingMs { get; set; }

    public bool IsActive { get; set; }

    [ForeignKey("MachineCode")]
    [InverseProperty("MachineTagMappings")]
    public virtual Machine MachineCodeNavigation { get; set; } = null!;
}
