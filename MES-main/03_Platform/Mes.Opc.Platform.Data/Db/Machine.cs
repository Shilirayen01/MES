using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Mes.Opc.Platform.Data.Db;

[Table("Machine")]
public partial class Machine
{
    [Key]
    [StringLength(50)]
    public string MachineCode { get; set; } = null!;

    [StringLength(100)]
    public string Name { get; set; } = null!;

    [StringLength(50)]
    public string? LineCode { get; set; }

    public int OpcEndpointId { get; set; }

    public bool IsActive { get; set; }

    [InverseProperty("MachineCodeNavigation")]
    public virtual ICollection<MachineTagMapping> MachineTagMappings { get; set; } = new List<MachineTagMapping>();

    [ForeignKey("OpcEndpointId")]
    [InverseProperty("Machines")]
    public virtual OpcEndpoint OpcEndpoint { get; set; } = null!;
}
