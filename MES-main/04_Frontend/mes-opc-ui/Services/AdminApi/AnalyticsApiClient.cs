using System.Net.Http.Json;

namespace mes_opc_ui.Services.AdminApi;

public sealed class AnalyticsApiClient(HttpClient http)
{
    // ── Definitions ──────────────────────────────────────────────────────────

    public Task<List<AnalyticsSummaryDefDto>?> GetDefinitionsAsync(bool? isActive = null)
    {
        var url = isActive.HasValue
            ? $"api/v1/analytics/summary/definitions?isActive={isActive.Value.ToString().ToLowerInvariant()}"
            : "api/v1/analytics/summary/definitions";
        return http.GetFromJsonAsync<List<AnalyticsSummaryDefDto>>(url);
    }

    public Task<AnalyticsSummaryDefDto?> GetDefinitionAsync(int id)
        => http.GetFromJsonAsync<AnalyticsSummaryDefDto>($"api/v1/analytics/summary/definitions/{id}");

    public async Task<AnalyticsSummaryDefDto?> CreateDefinitionAsync(AnalyticsSummaryDefCreateDto dto)
    {
        var res = await http.PostAsJsonAsync("api/v1/analytics/summary/definitions", dto);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<AnalyticsSummaryDefDto>();
    }

    public async Task DeleteDefinitionAsync(int id)
    {
        var res = await http.DeleteAsync($"api/v1/analytics/summary/definitions/{id}");
        res.EnsureSuccessStatusCode();
    }

    // ── Items ────────────────────────────────────────────────────────────────

    public async Task<AnalyticsSummaryItemDto?> AddItemAsync(int defId, AnalyticsSummaryItemCreateDto dto)
    {
        var res = await http.PostAsJsonAsync($"api/v1/analytics/summary/definitions/{defId}/items", dto);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<AnalyticsSummaryItemDto>();
    }

    public async Task DeleteItemAsync(int defId, int itemId)
    {
        var res = await http.DeleteAsync($"api/v1/analytics/summary/definitions/{defId}/items/{itemId}");
        res.EnsureSuccessStatusCode();
    }
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

public sealed class AnalyticsSummaryDefDto
{
    public int SummaryId { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; }
    public string? AppliesToMachineCode { get; set; }
    public List<AnalyticsSummaryItemDto> Items { get; set; } = new();
}

public sealed class AnalyticsSummaryDefCreateDto
{
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public string? AppliesToMachineCode { get; set; }
}

public sealed class AnalyticsSummaryItemDto
{
    public int ItemId { get; set; }
    public int SummaryId { get; set; }
    public string FieldName { get; set; } = "";
    public string SourceType { get; set; } = "Tag";
    public string? TagNodeId { get; set; }
    public string? Scope { get; set; }
    public string? Aggregation { get; set; }
    public bool? IsCumulative { get; set; }
    public double? ConstantValue { get; set; }
    public int? LookbackMinutes { get; set; }
    public int? MaxGapSeconds { get; set; }
    public string? Unit { get; set; }
}

public sealed class AnalyticsSummaryItemCreateDto
{
    public string FieldName { get; set; } = "";
    public string SourceType { get; set; } = "Tag";
    public string? TagNodeId { get; set; }
    public string? Scope { get; set; }
    public string? Aggregation { get; set; }
    public bool? IsCumulative { get; set; }
    public double? ConstantValue { get; set; }
    public int? LookbackMinutes { get; set; }
    public int? MaxGapSeconds { get; set; }
    public string? Unit { get; set; }
}
