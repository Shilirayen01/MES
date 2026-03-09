using System.Net.Http.Json;

namespace mes_opc_ui.Services.AdminApi;

public sealed class TagMappingsApiClient(HttpClient http)
{
    public Task<List<MachineTagMappingDto>?> GetByMachineAsync(string machineCode, bool? isActive = null)
    {
        var url = isActive is null
            ? $"api/v1/machines/{Uri.EscapeDataString(machineCode)}/tag-mappings"
            : $"api/v1/machines/{Uri.EscapeDataString(machineCode)}/tag-mappings?isActive={isActive.Value.ToString().ToLowerInvariant()}";

        return http.GetFromJsonAsync<List<MachineTagMappingDto>>(url);
    }

    public async Task<MachineTagMappingDto?> CreateAsync(string machineCode, MachineTagMappingCreateDto dto)
    {
        var res = await http.PostAsJsonAsync($"api/v1/machines/{Uri.EscapeDataString(machineCode)}/tag-mappings", dto);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<MachineTagMappingDto>();
    }

    public async Task<MachineTagMappingDto?> UpdateAsync(int id, MachineTagMappingUpdateDto dto)
    {
        var res = await http.PutAsJsonAsync($"api/v1/tag-mappings/{id}", dto);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<MachineTagMappingDto>();
    }

    public async Task DeleteAsync(int id)
    {
        var res = await http.DeleteAsync($"api/v1/tag-mappings/{id}");
        res.EnsureSuccessStatusCode();
    }
}

public sealed class MachineTagMappingDto
{
    public int Id { get; set; }
    public string MachineCode { get; set; } = "";
    public string OpcNodeId { get; set; } = "";
    public string CharacteristicCode { get; set; } = "";
    public string DataTypeExpected { get; set; } = "";
    public string? Unit { get; set; }
    public double? MinValue { get; set; }
    public double? MaxValue { get; set; }
    public int? SamplingMs { get; set; }
    public int? PublishingMs { get; set; }
    public bool IsActive { get; set; }
}

public class MachineTagMappingCreateDto
{
    public string OpcNodeId { get; set; } = "";
    public string CharacteristicCode { get; set; } = "";
    public string DataTypeExpected { get; set; } = "";
    public string? Unit { get; set; }
    public double? MinValue { get; set; }
    public double? MaxValue { get; set; }
    public int? SamplingMs { get; set; }
    public int? PublishingMs { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class MachineTagMappingUpdateDto : MachineTagMappingCreateDto { }
