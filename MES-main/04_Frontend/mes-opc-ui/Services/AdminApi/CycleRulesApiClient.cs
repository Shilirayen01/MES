using System.Net.Http.Json;

namespace mes_opc_ui.Services.AdminApi;

public sealed class CycleRulesApiClient(HttpClient http)
{
    public Task<List<CycleRuleDto>?> GetAllAsync(string machineCode)
        => http.GetFromJsonAsync<List<CycleRuleDto>>(
            $"api/v1/machines/{Uri.EscapeDataString(machineCode)}/cycle-rules");

    public async Task<CycleRuleDto?> CreateAsync(string machineCode, CycleRuleCreateDto dto)
    {
        var res = await http.PostAsJsonAsync(
            $"api/v1/machines/{Uri.EscapeDataString(machineCode)}/cycle-rules", dto);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<CycleRuleDto>();
    }

    public async Task<CycleRuleDto?> UpdateAsync(string machineCode, int id, CycleRuleUpdateDto dto)
    {
        var res = await http.PutAsJsonAsync(
            $"api/v1/machines/{Uri.EscapeDataString(machineCode)}/cycle-rules/{id}", dto);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<CycleRuleDto>();
    }

    public async Task DeleteAsync(string machineCode, int id)
    {
        var res = await http.DeleteAsync(
            $"api/v1/machines/{Uri.EscapeDataString(machineCode)}/cycle-rules/{id}");
        res.EnsureSuccessStatusCode();
    }
}

// ── DTOs (inline, same pattern as MachinesApiClient) ─────────────────────────

public sealed class CycleRuleDto
{
    public int Id { get; set; }
    public string MachineCode { get; set; } = "";
    public string ScopeKey { get; set; } = "";
    public bool IsActive { get; set; }
    public string StartStrategy { get; set; } = "";
    public string? StartNodeId { get; set; }
    public string? StartEdgeType { get; set; }
    public string? StartValue { get; set; }
    public string EndPrimaryStrategy { get; set; } = "";
    public string? EndPrimaryNodeId { get; set; }
    public string? EndPrimaryEdgeType { get; set; }
    public string? EndFallbackStrategy { get; set; }
    public string? EndFallbackNodeId { get; set; }
    public string? AbortNodeIds { get; set; }
    public int DebounceMs { get; set; }
    public int MinCycleSeconds { get; set; }
    public int TimeoutSeconds { get; set; }
    public decimal? Epsilon { get; set; }
    public decimal? TargetTolerance { get; set; }
    public string? ValidationSpeedNodeId { get; set; }
    public decimal? ValidationSpeedMin { get; set; }
    public string? ValidationStateNodeId { get; set; }
    public string? ValidationStateValue { get; set; }
    public string? RecoveryStrategy { get; set; }
    public string? RecoveryConfirmNodeId { get; set; }
    public decimal? RecoveryConfirmDelta { get; set; }
    public int? RecoveryConfirmWindowSeconds { get; set; }
}

public sealed class CycleRuleCreateDto
{
    public string ScopeKey { get; set; } = "default";
    public bool IsActive { get; set; } = true;
    public string StartStrategy { get; set; } = "EdgeOnBit";
    public string? StartNodeId { get; set; }
    public string? StartEdgeType { get; set; } = "Rising";
    public string? StartValue { get; set; }
    public string EndPrimaryStrategy { get; set; } = "EdgeOnBit";
    public string? EndPrimaryNodeId { get; set; }
    public string? EndPrimaryEdgeType { get; set; } = "Falling";
    public string? EndFallbackStrategy { get; set; }
    public string? EndFallbackNodeId { get; set; }
    public string? AbortNodeIds { get; set; }
    public int DebounceMs { get; set; } = 500;
    public int MinCycleSeconds { get; set; } = 1;
    public int TimeoutSeconds { get; set; } = 3600;
    public decimal? Epsilon { get; set; }
    public decimal? TargetTolerance { get; set; }
    public string? ValidationSpeedNodeId { get; set; }
    public decimal? ValidationSpeedMin { get; set; }
    public string? ValidationStateNodeId { get; set; }
    public string? ValidationStateValue { get; set; }
    public string? RecoveryStrategy { get; set; }
    public string? RecoveryConfirmNodeId { get; set; }
    public decimal? RecoveryConfirmDelta { get; set; }
    public int? RecoveryConfirmWindowSeconds { get; set; }
}

public sealed class CycleRuleUpdateDto
{
    public string ScopeKey { get; set; } = "default";
    public bool IsActive { get; set; }
    public string StartStrategy { get; set; } = "";
    public string? StartNodeId { get; set; }
    public string? StartEdgeType { get; set; }
    public string? StartValue { get; set; }
    public string EndPrimaryStrategy { get; set; } = "";
    public string? EndPrimaryNodeId { get; set; }
    public string? EndPrimaryEdgeType { get; set; }
    public string? EndFallbackStrategy { get; set; }
    public string? EndFallbackNodeId { get; set; }
    public string? AbortNodeIds { get; set; }
    public int DebounceMs { get; set; }
    public int MinCycleSeconds { get; set; }
    public int TimeoutSeconds { get; set; }
    public decimal? Epsilon { get; set; }
    public decimal? TargetTolerance { get; set; }
    public string? ValidationSpeedNodeId { get; set; }
    public decimal? ValidationSpeedMin { get; set; }
    public string? ValidationStateNodeId { get; set; }
    public string? ValidationStateValue { get; set; }
    public string? RecoveryStrategy { get; set; }
    public string? RecoveryConfirmNodeId { get; set; }
    public decimal? RecoveryConfirmDelta { get; set; }
    public int? RecoveryConfirmWindowSeconds { get; set; }
}
