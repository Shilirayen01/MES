using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Mes.Opc.Platform.Data.Db;

[Table("OpcEndpoint")]
public partial class OpcEndpoint
{
    [Key]
    public int Id { get; set; }

    [StringLength(100)]
    public string Name { get; set; } = null!;

    [StringLength(200)]
    public string EndpointUrl { get; set; } = null!;

    public bool IsActive { get; set; }

    [StringLength(255)]
    public string? Description { get; set; }

    public DateTime CreatedDate { get; set; }

    [StringLength(100)]
    public string? CreatedBy { get; set; }

    [InverseProperty("OpcEndpoint")]
    public virtual ICollection<Machine> Machines { get; set; } = new List<Machine>();
}
