using System.ComponentModel.DataAnnotations;

namespace Mes.Opc.Platform.Api.Dtos;

public sealed class MachineCreateDto
{
    [Required, StringLength(50)]
    public string MachineCode { get; set; } = null!;

    [Required, StringLength(100)]
    public string Name { get; set; } = null!;

    [StringLength(50)]
    public string? LineCode { get; set; }

    [Required]
    public int OpcEndpointId { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class MachineUpdateDto
{
    [Required, StringLength(100)]
    public string Name { get; set; } = null!;

    [StringLength(50)]
    public string? LineCode { get; set; }

    [Required]
    public int OpcEndpointId { get; set; }

    public bool IsActive { get; set; }
}
