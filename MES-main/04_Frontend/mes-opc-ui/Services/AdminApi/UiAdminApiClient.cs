using System.Net.Http.Json;

namespace mes_opc_ui.Services.AdminApi;

public sealed class UiAdminApiClient(HttpClient http)
{
    // ---------------- Dashboards ----------------
    public Task<List<UiDashboardDto>?> GetDashboardsAsync()
        => http.GetFromJsonAsync<List<UiDashboardDto>>("api/v1/ui/dashboards");

    public async Task<UiDashboardDto?> CreateDashboardAsync(UiDashboardCreateDto dto)
    {
        var res = await http.PostAsJsonAsync("api/v1/ui/dashboards", dto);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<UiDashboardDto>();
    }

    public async Task<UiDashboardDto?> UpdateDashboardAsync(Guid id, UiDashboardUpdateDto dto)
    {
        var res = await http.PutAsJsonAsync($"api/v1/ui/dashboards/{id}", dto);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<UiDashboardDto>();
    }

    public async Task SetDefaultDashboardAsync(Guid id)
    {
        var res = await http.PostAsync($"api/v1/ui/dashboards/{id}/set-default", null);
        res.EnsureSuccessStatusCode();
    }

    public async Task DeleteDashboardAsync(Guid id)
    {
        var res = await http.DeleteAsync($"api/v1/ui/dashboards/{id}");
        res.EnsureSuccessStatusCode();
    }

    // ---------------- Zones ----------------
    public Task<List<UiZoneDto>?> GetZonesAsync(Guid dashboardId)
        => http.GetFromJsonAsync<List<UiZoneDto>>($"api/v1/ui/dashboards/{dashboardId}/zones");

    public async Task<UiZoneDto?> CreateZoneAsync(Guid dashboardId, UiZoneCreateDto dto)
    {
        var res = await http.PostAsJsonAsync($"api/v1/ui/dashboards/{dashboardId}/zones", dto);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<UiZoneDto>();
    }

    public async Task<UiZoneDto?> UpdateZoneAsync(Guid id, UiZoneUpdateDto dto)
    {
        var res = await http.PutAsJsonAsync($"api/v1/ui/zones/{id}", dto);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<UiZoneDto>();
    }

    public async Task DeleteZoneAsync(Guid id)
    {
        var res = await http.DeleteAsync($"api/v1/ui/zones/{id}");
        res.EnsureSuccessStatusCode();
    }

    // ---------------- Widgets ----------------
    public Task<List<UiWidgetDto>?> GetWidgetsAsync(Guid zoneId)
        => http.GetFromJsonAsync<List<UiWidgetDto>>($"api/v1/ui/zones/{zoneId}/widgets");

    public async Task<UiWidgetDto?> CreateWidgetAsync(Guid zoneId, UiWidgetCreateDto dto)
    {
        var res = await http.PostAsJsonAsync($"api/v1/ui/zones/{zoneId}/widgets", dto);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<UiWidgetDto>();
    }

    public async Task<UiWidgetDto?> UpdateWidgetAsync(Guid id, UiWidgetUpdateDto dto)
    {
        var res = await http.PutAsJsonAsync($"api/v1/ui/widgets/{id}", dto);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<UiWidgetDto>();
    }

    public async Task DeleteWidgetAsync(Guid id)
    {
        var res = await http.DeleteAsync($"api/v1/ui/widgets/{id}");
        res.EnsureSuccessStatusCode();
    }

    // ---------------- Bindings ----------------
    public Task<List<UiWidgetBindingDto>?> GetBindingsAsync(Guid widgetId)
        => http.GetFromJsonAsync<List<UiWidgetBindingDto>>($"api/v1/ui/widgets/{widgetId}/bindings");

    public async Task<UiWidgetBindingDto?> CreateBindingAsync(Guid widgetId, UiWidgetBindingCreateDto dto)
    {
        var res = await http.PostAsJsonAsync($"api/v1/ui/widgets/{widgetId}/bindings", dto);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<UiWidgetBindingDto>();
    }

    public async Task<UiWidgetBindingDto?> UpdateBindingAsync(Guid id, UiWidgetBindingUpdateDto dto)
    {
        var res = await http.PutAsJsonAsync($"api/v1/ui/bindings/{id}", dto);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<UiWidgetBindingDto>();
    }

    public async Task DeleteBindingAsync(Guid id)
    {
        var res = await http.DeleteAsync($"api/v1/ui/bindings/{id}");
        res.EnsureSuccessStatusCode();
    }
}

// ---------------- DTOs ----------------
public sealed class UiDashboardDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

public class UiDashboardCreateDto
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
}
public sealed class UiDashboardUpdateDto : UiDashboardCreateDto { }

public sealed class UiZoneDto
{
    public Guid Id { get; set; }
    public Guid DashboardId { get; set; }
    public string Title { get; set; } = "";
    public string LayoutType { get; set; } = "";
    public string? PropsJson { get; set; }
    public int OrderIndex { get; set; }
}

public class UiZoneCreateDto
{
    public string Title { get; set; } = "";
    public string LayoutType { get; set; } = "";
    public string? PropsJson { get; set; }
    public int OrderIndex { get; set; } = 0;
}
public sealed class UiZoneUpdateDto : UiZoneCreateDto { }

public sealed class UiWidgetDto
{
    public Guid Id { get; set; }
    public Guid ZoneId { get; set; }
    public string Title { get; set; } = "";
    public string WidgetType { get; set; } = "";
    public string? PropsJson { get; set; }
    public int OrderIndex { get; set; }
}

public class UiWidgetCreateDto
{
    public string Title { get; set; } = "";
    public string WidgetType { get; set; } = "";
    public string? PropsJson { get; set; }
    public int OrderIndex { get; set; } = 0;
}
public sealed class UiWidgetUpdateDto : UiWidgetCreateDto { }

public sealed class UiWidgetBindingDto
{
    public Guid Id { get; set; }
    public Guid WidgetId { get; set; }
    public string MachineCode { get; set; } = "";
    public string OpcNodeId { get; set; } = "";
    public string BindingRole { get; set; } = "";
}

public class UiWidgetBindingCreateDto
{
    public string MachineCode { get; set; } = "";
    public string OpcNodeId { get; set; } = "";
    public string BindingRole { get; set; } = "";
}
public sealed class UiWidgetBindingUpdateDto : UiWidgetBindingCreateDto { }
