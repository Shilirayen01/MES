using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Mes.Opc.Platform.Data.Db;

[Table("MachineTagValue")]
[Index("MachineCode", "SourceTimestamp", Name = "IX_MachineTagValue_Machine_Time")]
public partial class MachineTagValue
{
    [Key]
    public long Id { get; set; }

    [StringLength(50)]
    public string MachineCode { get; set; } = null!;

    [StringLength(200)]
    public string OpcNodeId { get; set; } = null!;

    [StringLength(4000)]
    public string? Value { get; set; }

    [StringLength(50)]
    public string StatusCode { get; set; } = null!;

    public DateTime? SourceTimestamp { get; set; }

    public DateTime CreatedDate { get; set; }
}
