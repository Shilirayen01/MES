// -------------------------------------------------------------------------------------------------
// Program.cs
//
// Entry point for the MES OPC REST API.
//
// This host exposes HTTP endpoints used by the UI and administrative tools:
// - /api/machines
// - /api/machines/{machineCode}/tags
// - /api/ui/dashboards/default
// - /api/ui/dashboards/{dashboardId}
//
// The realtime gateway (Mes.Opc.Platform.Realtime) is responsible for SignalR (/opcHub)
// and streaming live tag/widget updates.
// -------------------------------------------------------------------------------------------------

using System;
using System.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mes.Opc.Acquisition.Runtime.Configuration;
using Mes.Opc.Platform.Data.Repository;
using Mes.Opc.Platform.Data.Db;
using Microsoft.EntityFrameworkCore;

namespace Mes.Opc.Platform.Api
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // CORS (dev-friendly): allow the UI (Blazor WASM) to call this API.
            // For production, replace SetIsOriginAllowed with WithOrigins("https://your-ui-host").
            builder.Services.AddCors(o =>
            {
                o.AddPolicy("DevCors", p => p
                    .SetIsOriginAllowed(_ => true)
                    .AllowAnyHeader()
                    .AllowAnyMethod());
            });

            // Connection string resolution:
            // - EF Core DbContext prefers "OpcDb"
            // - Some legacy services used "DefaultConnection"
            // Keep a fallback so the API can run even if only one key is provided.
            var cs = builder.Configuration.GetConnectionString("OpcDb")
                     ?? builder.Configuration.GetConnectionString("DefaultConnection")
                     ?? throw new InvalidOperationException("Missing ConnectionStrings:OpcDb (or DefaultConnection).");

            // EF Core DbContext (Database-First scaffold)
            builder.Services.AddDbContext<OpcDbContext>(options => options.UseSqlServer(cs));

            // DB connection factory (used by ConfigLoader / legacy DAL)
            builder.Services.AddScoped<IDbConnection>(_ => new SqlConnection(cs));

            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            // Data access services
            builder.Services.AddScoped<ConfigLoader>();
            builder.Services.AddScoped<UiConfigurationRepository>();
            builder.Services.AddSwaggerGen();
            var app = builder.Build();

            app.UseRouting();
            app.UseCors("DevCors");
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }


            // -----------------------------------------------------------------------------
            // REST endpoints
            // -----------------------------------------------------------------------------

            // Catalogue APIs (machines, tags)
            app.MapGet("/api/machines", async (ConfigLoader loader) =>
            {
                return await loader.LoadMachinesAsync();
            });

            app.MapGet("/api/machines/{machineCode}/tags", async (ConfigLoader loader, string machineCode) =>
            {
                var tags = await loader.LoadTagMappingsAsync();
                return tags.Where(t => t.MachineCode.Equals(machineCode, StringComparison.OrdinalIgnoreCase)).ToList();
            });

            // Dynamic UI configuration (dashboards/zones/widgets/bindings)
            app.MapGet("/api/ui/dashboards/default", async (UiConfigurationRepository repo) =>
            {
                var dto = await repo.GetDefaultDashboardAsync();
                return dto is null ? Results.NotFound() : Results.Ok(dto);
            });

            app.MapGet("/api/ui/dashboards/{dashboardId:guid}", async (UiConfigurationRepository repo, Guid dashboardId) =>
            {
                var dto = await repo.GetDashboardAsync(dashboardId);
                return dto is null ? Results.NotFound() : Results.Ok(dto);
            });
            app.MapControllers();
            app.Run();
        }
    }
}
