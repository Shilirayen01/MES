using System.Net.Http.Json;

namespace mes_opc_ui.Services.AdminApi;

public sealed class OpcEndpointsApiClient(HttpClient http)
{
    public Task<List<OpcEndpointDto>?> GetAllAsync(bool? isActive = null)
        => http.GetFromJsonAsync<List<OpcEndpointDto>>(
            isActive is null ? "api/v1/opc-endpoints" : $"api/v1/opc-endpoints?isActive={isActive.Value.ToString().ToLowerInvariant()}");

    public async Task<OpcEndpointDto?> CreateAsync(OpcEndpointCreateDto dto)
    {
        var res = await http.PostAsJsonAsync("api/v1/opc-endpoints", dto);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<OpcEndpointDto>();
    }

    public async Task<OpcEndpointDto?> UpdateAsync(int id, OpcEndpointUpdateDto dto)
    {
        var res = await http.PutAsJsonAsync($"api/v1/opc-endpoints/{id}", dto);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<OpcEndpointDto>();
    }

    public async Task DeleteAsync(int id)
    {
        var res = await http.DeleteAsync($"api/v1/opc-endpoints/{id}");
        res.EnsureSuccessStatusCode();
    }
}

public sealed class OpcEndpointDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string EndpointUrl { get; set; } = "";
    public bool IsActive { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? CreatedBy { get; set; }
}

public sealed class OpcEndpointCreateDto
{
    public string Name { get; set; } = "";
    public string EndpointUrl { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
    public string? CreatedBy { get; set; }
}

public sealed class OpcEndpointUpdateDto
{
    public string Name { get; set; } = "";
    public string EndpointUrl { get; set; } = "";
    public bool IsActive { get; set; }
    public string? Description { get; set; }
}
