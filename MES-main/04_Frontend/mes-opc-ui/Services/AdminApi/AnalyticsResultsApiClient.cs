using System.Net.Http.Json;

namespace mes_opc_ui.Services.AdminApi;

public sealed class AnalyticsResultsApiClient(HttpClient http)
{
    public async Task<List<ProductionRunResultDto>?> GetResultsAsync(
        string machineCode,
        DateTime? from = null,
        DateTime? to = null,
        int limit = 50)
    {
        var qs = new List<string>();
        if (from.HasValue) qs.Add($"from={from.Value:yyyy-MM-ddTHH:mm:ss}");
        if (to.HasValue) qs.Add($"to={to.Value:yyyy-MM-ddTHH:mm:ss}");
        qs.Add($"limit={limit}");

        var url = $"api/v1/analytics/results/{Uri.EscapeDataString(machineCode)}?{string.Join("&", qs)}";
        return await http.GetFromJsonAsync<List<ProductionRunResultDto>>(url);
    }
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

public sealed class ProductionRunResultDto
{
    public Guid RunId { get; set; }
    public string MachineCode { get; set; } = "";
    public string ScopeKey { get; set; } = "";
    public DateTime StartTs { get; set; }
    public DateTime? EndTs { get; set; }
    public string Status { get; set; } = "";
    public string? EndReason { get; set; }
    public int? DurationSeconds { get; set; }
    public List<RunSummaryValueDto> SummaryValues { get; set; } = new();
    public List<RunKpiValueDto> KpiValues { get; set; } = new();
}

public sealed class RunSummaryValueDto
{
    public string FieldName { get; set; } = "";
    public double? Value { get; set; }
    public string? Unit { get; set; }
    public DateTime? ComputedAt { get; set; }
}

public sealed class RunKpiValueDto
{
    public string KpiCode { get; set; } = "";
    public double? Value { get; set; }
    public string? Unit { get; set; }
    public DateTime? ComputedAt { get; set; }
}
