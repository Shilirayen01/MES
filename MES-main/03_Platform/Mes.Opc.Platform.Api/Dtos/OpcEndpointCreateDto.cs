using System.ComponentModel.DataAnnotations;

namespace Mes.Opc.Platform.Api.Dtos;

public sealed class OpcEndpointCreateDto
{
    [Required, StringLength(100)]
    public string Name { get; set; } = null!;

    [Required, StringLength(200)]
    public string EndpointUrl { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    [StringLength(255)]
    public string? Description { get; set; }

    [StringLength(100)]
    public string? CreatedBy { get; set; }
}

public sealed class OpcEndpointUpdateDto
{
    [Required, StringLength(100)]
    public string Name { get; set; } = null!;

    [Required, StringLength(200)]
    public string EndpointUrl { get; set; } = null!;

    public bool IsActive { get; set; }

    [StringLength(255)]
    public string? Description { get; set; }
}
