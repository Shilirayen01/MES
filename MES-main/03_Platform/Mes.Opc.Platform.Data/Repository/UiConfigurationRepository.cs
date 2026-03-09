using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Mes.Opc.Contracts.Dtos;

namespace Mes.Opc.Platform.Data.Repository
{
    /// <summary>
    /// Loads MES UI configuration (dashboards/zones/widgets/bindings) from SQL Server.
    ///
    /// The UI is "dynamic" because the front-end renders whatever is described
    /// in these tables instead of hardcoding screens.
    /// </summary>
    public sealed class UiConfigurationRepository
    {
        private readonly IDbConnection _db;

        public UiConfigurationRepository(IDbConnection db) => _db = db;

        /// <summary>
        /// Returns the default dashboard (IsDefault = 1) if it exists.
        /// If none is marked as default, returns the first dashboard by name.
        /// </summary>
        public async Task<DashboardDto?> GetDefaultDashboardAsync()
        {
            var dashboard = await _db.QuerySingleOrDefaultAsync<DashboardRow>(
                "SELECT TOP(1) Id, Name FROM UiDashboard WHERE IsDefault = 1 ORDER BY Name");

            dashboard ??= await _db.QuerySingleOrDefaultAsync<DashboardRow>(
                "SELECT TOP(1) Id, Name FROM UiDashboard ORDER BY Name");

            if (dashboard is null)
                return null;

            return await GetDashboardAsync(dashboard.Id);
        }

        /// <summary>
        /// Loads one dashboard and all its zones/widgets/bindings.
        /// </summary>
        public async Task<DashboardDto?> GetDashboardAsync(Guid dashboardId)
        {
            var dash = await _db.QuerySingleOrDefaultAsync<DashboardRow>(
                "SELECT Id, Name FROM UiDashboard WHERE Id = @Id",
                new { Id = dashboardId });

            if (dash is null)
                return null;

            var zones = (await _db.QueryAsync<ZoneRow>(
                    "SELECT Id, DashboardId, Title, LayoutType, PropsJson, OrderIndex " +
                    "FROM UiZone WHERE DashboardId = @DashboardId ORDER BY OrderIndex",
                    new { DashboardId = dashboardId }))
                .ToList();

            var zoneIds = zones.Select(z => z.Id).ToArray();
            var widgets = zoneIds.Length == 0
                ? new List<WidgetRow>()
                : (await _db.QueryAsync<WidgetRow>(
                        "SELECT Id, ZoneId, Title, WidgetType, PropsJson, OrderIndex " +
                        "FROM UiWidget WHERE ZoneId IN @ZoneIds ORDER BY OrderIndex",
                        new { ZoneIds = zoneIds }))
                    .ToList();

            var widgetIds = widgets.Select(w => w.Id).ToArray();
            var bindings = widgetIds.Length == 0
                ? new List<BindingRow>()
                : (await _db.QueryAsync<BindingRow>(
                        "SELECT Id, WidgetId, MachineCode, OpcNodeId, BindingRole " +
                        "FROM UiWidgetBinding WHERE WidgetId IN @WidgetIds",
                        new { WidgetIds = widgetIds }))
                    .ToList();

            // Build hierarchical DTO
            var dashboardDto = new DashboardDto
            {
                Id = dash.Id,
                Name = dash.Name,
                Zones = zones.Select(z => new ZoneDto
                {
                    Id = z.Id,
                    DashboardId = z.DashboardId,
                    Title = z.Title,
                    LayoutType = z.LayoutType,
                    PropsJson = z.PropsJson,
                    Widgets = widgets
                        .Where(w => w.ZoneId == z.Id)
                        .Select(w => new WidgetDto
                        {
                            Id = w.Id,
                            ZoneId = w.ZoneId,
                            Title = w.Title,
                            WidgetType = w.WidgetType,
                            PropsJson = w.PropsJson,
                            Bindings = bindings
                                .Where(b => b.WidgetId == w.Id)
                                .Select(b => new WidgetBindingDto
                                {
                                    Id = b.Id,
                                    WidgetId = b.WidgetId,
                                    MachineCode = b.MachineCode,
                                    OpcNodeId = b.OpcNodeId,
                                    BindingRole = b.BindingRole
                                })
                                .ToList()
                        })
                        .ToList()
                }).ToList()
            };

            return dashboardDto;
        }

        /// <summary>
        /// Loads all widget bindings from SQL.
        ///
        /// This is used by the realtime gateway to build an in-memory routing map
        /// from (MachineCode, OpcNodeId) to widget targets.
        /// </summary>
        public async Task<IReadOnlyList<WidgetBindingFlatRow>> GetAllWidgetBindingsAsync()
        {
            var rows = await _db.QueryAsync<WidgetBindingFlatRow>(
                "SELECT WidgetId, MachineCode, OpcNodeId, BindingRole FROM UiWidgetBinding");
            return rows.ToList();
        }

        // Lightweight row records for Dapper mapping
        private sealed record DashboardRow(Guid Id, string Name);

        private sealed record ZoneRow(
            Guid Id,
            Guid DashboardId,
            string Title,
            string LayoutType,
            string? PropsJson,
            int OrderIndex);

        private sealed record WidgetRow(
            Guid Id,
            Guid ZoneId,
            string Title,
            string WidgetType,
            string? PropsJson,
            int OrderIndex);

        private sealed record BindingRow(
            Guid Id,
            Guid WidgetId,
            string MachineCode,
            string OpcNodeId,
            string BindingRole);

        /// <summary>
        /// Flat binding record for fast routing map construction.
        /// </summary>
        public sealed record WidgetBindingFlatRow(
            Guid WidgetId,
            string MachineCode,
            string OpcNodeId,
            string BindingRole);
    }
}
