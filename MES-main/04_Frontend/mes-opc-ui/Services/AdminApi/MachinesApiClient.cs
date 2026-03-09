using System.Net.Http.Json;

namespace mes_opc_ui.Services.AdminApi;

public sealed class MachinesApiClient(HttpClient http)
{
    public Task<List<MachineDto>?> GetAllAsync(bool? isActive = null, string? lineCode = null, int? opcEndpointId = null)
    {
        var qs = new List<string>();
        if (isActive is not null) qs.Add($"isActive={isActive.Value.ToString().ToLowerInvariant()}");
        if (!string.IsNullOrWhiteSpace(lineCode)) qs.Add($"lineCode={Uri.EscapeDataString(lineCode)}");
        if (opcEndpointId is not null) qs.Add($"opcEndpointId={opcEndpointId.Value}");
        var url = qs.Count == 0 ? "api/v1/machines" : "api/v1/machines?" + string.Join("&", qs);
        return http.GetFromJsonAsync<List<MachineDto>>(url);
    }

    public Task<MachineDto?> GetByCodeAsync(string machineCode)
        => http.GetFromJsonAsync<MachineDto>($"api/v1/machines/{Uri.EscapeDataString(machineCode)}");

    public async Task<MachineDto?> CreateAsync(MachineCreateDto dto)
    {
        var res = await http.PostAsJsonAsync("api/v1/machines", dto);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<MachineDto>();
    }

    public async Task<MachineDto?> UpdateAsync(string machineCode, MachineUpdateDto dto)
    {
        var res = await http.PutAsJsonAsync($"api/v1/machines/{Uri.EscapeDataString(machineCode)}", dto);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<MachineDto>();
    }

    public async Task DeleteAsync(string machineCode)
    {
        var res = await http.DeleteAsync($"api/v1/machines/{Uri.EscapeDataString(machineCode)}");
        res.EnsureSuccessStatusCode();
    }
}

public sealed class MachineDto
{
    public string MachineCode { get; set; } = "";
    public string Name { get; set; } = "";
    public string? LineCode { get; set; }
    public int OpcEndpointId { get; set; }
    public bool IsActive { get; set; }
}

public sealed class MachineCreateDto
{
    public string MachineCode { get; set; } = "";
    public string Name { get; set; } = "";
    public string? LineCode { get; set; }
    public int OpcEndpointId { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class MachineUpdateDto
{
    public string Name { get; set; } = "";
    public string? LineCode { get; set; }
    public int OpcEndpointId { get; set; }
    public bool IsActive { get; set; }
}
