using System.Net.Http.Json;
using Mes.Opc.Contracts.Dtos;

namespace mes_opc_ui.Services
{
    /// <summary>
    /// Client-side service that fetches dynamic dashboard configuration.
    /// The API is hosted by Mes.Opc.Platform.Api.
    /// </summary>
    public sealed class UiConfigurationService
    {
        private readonly HttpClient _http;

        public UiConfigurationService(HttpClient http) => _http = http;

        public async Task<DashboardDto?> GetDefaultDashboardAsync(CancellationToken ct = default)
        {
            // Returns 404 if nothing is configured in SQL.
            return await _http.GetFromJsonAsync<DashboardDto>("api/ui/dashboards/default", ct);
        }

        public async Task<DashboardDto?> GetDashboardAsync(Guid id, CancellationToken ct = default)
        {
            return await _http.GetFromJsonAsync<DashboardDto>($"api/ui/dashboards/{id}", ct);
        }
    }
}
