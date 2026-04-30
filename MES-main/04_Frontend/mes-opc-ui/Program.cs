using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using mes_opc_ui.Services;
using mes_opc_ui.Services.AdminApi;

namespace mes_opc_ui
{
    /// <summary>
    /// Blazor WebAssembly entry point.
    ///
    /// This UI is fully dynamic:
    /// - It downloads a dashboard definition from SQL (via the REST API)
    /// - It renders zones/widgets based on that definition
    /// - It connects to SignalR and updates widgets live
    /// </summary>
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");

            // REST API base URL.
            // If you host UI separately, set ApiBaseUrl in wwwroot/appsettings.json.
            var apiBaseUrl = builder.Configuration["ApiBaseUrl"];
            if (string.IsNullOrWhiteSpace(apiBaseUrl))
                apiBaseUrl = builder.HostEnvironment.BaseAddress;

            // SignalR base URL. Defaults to ApiBaseUrl for backwards compatibility.
            var realtimeBaseUrl = builder.Configuration["RealtimeBaseUrl"];

            var endpoints = new ClientEndpoints(apiBaseUrl!, realtimeBaseUrl);
            builder.Services.AddSingleton(endpoints);

            // Default HttpClient is used by UiConfigurationService (REST calls)
            builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(endpoints.ApiBaseUrl) });

            builder.Services.AddScoped<UiConfigurationService>();
            builder.Services.AddScoped<RealtimeHubService>();
            builder.Services.AddScoped<LocalizationService>();
            builder.Services.AddSingleton<WidgetValueStore>();
            builder.Services.AddSingleton<ActivityLogService>();
            builder.Services.AddAdminApiClients(builder.Configuration);

            await builder.Build().RunAsync();
        }
    }
}
