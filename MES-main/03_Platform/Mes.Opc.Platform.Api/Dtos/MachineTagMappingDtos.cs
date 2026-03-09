using System.ComponentModel.DataAnnotations;

namespace Mes.Opc.Platform.Api.Dtos;

public class MachineTagMappingCreateDto
{
    [Required, StringLength(200)]
    public string OpcNodeId { get; set; } = null!;

    [Required, StringLength(100)]
    public string CharacteristicCode { get; set; } = null!;

    [Required, StringLength(50)]
    public string DataTypeExpected { get; set; } = null!;

    [StringLength(20)]
    public string? Unit { get; set; }

    public double? MinValue { get; set; }
    public double? MaxValue { get; set; }

    public int? SamplingMs { get; set; }
    public int? PublishingMs { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class MachineTagMappingUpdateDto : MachineTagMappingCreateDto
{
}
