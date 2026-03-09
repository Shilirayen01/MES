using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace mes_opc_ui.Services.AdminApi;

public static class AdminApiServiceCollectionExtensions
{
    public static IServiceCollection AddAdminApiClients(this IServiceCollection services, IConfiguration config)
    {
        // Essaie plusieurs clés possibles (selon ton UI actuel)
        var baseUrl =
            config["ApiBaseUrl"]
            ?? config["Api:BaseUrl"]
            ?? config.GetSection("AdminApi")["BaseUrl"]
            ?? "https://localhost:61818";

        services.Configure<AdminApiOptions>(o => o.BaseUrl = baseUrl);

        services.AddHttpClient<OpcEndpointsApiClient>((sp, http) =>
        {
            var opt = sp.GetRequiredService<IOptions<AdminApiOptions>>().Value;
            http.BaseAddress = new Uri(opt.BaseUrl.TrimEnd('/') + "/");
        });

        services.AddHttpClient<MachinesApiClient>((sp, http) =>
        {
            var opt = sp.GetRequiredService<IOptions<AdminApiOptions>>().Value;
            http.BaseAddress = new Uri(opt.BaseUrl.TrimEnd('/') + "/");
        });

        services.AddHttpClient<TagMappingsApiClient>((sp, http) =>
        {
            var opt = sp.GetRequiredService<IOptions<AdminApiOptions>>().Value;
            http.BaseAddress = new Uri(opt.BaseUrl.TrimEnd('/') + "/");
        });

        services.AddHttpClient<UiAdminApiClient>((sp, http) =>
        {
            var opt = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AdminApiOptions>>().Value;
            http.BaseAddress = new Uri(opt.BaseUrl.TrimEnd('/') + "/");
        });

        services.AddHttpClient<CycleRulesApiClient>((sp, http) =>
        {
            var opt = sp.GetRequiredService<IOptions<AdminApiOptions>>().Value;
            http.BaseAddress = new Uri(opt.BaseUrl.TrimEnd('/') + "/");
        });

        services.AddHttpClient<AnalyticsApiClient>((sp, http) =>
        {
            var opt = sp.GetRequiredService<IOptions<AdminApiOptions>>().Value;
            http.BaseAddress = new Uri(opt.BaseUrl.TrimEnd('/') + "/");
        });

        services.AddHttpClient<AnalyticsResultsApiClient>((sp, http) =>
        {
            var opt = sp.GetRequiredService<IOptions<AdminApiOptions>>().Value;
            http.BaseAddress = new Uri(opt.BaseUrl.TrimEnd('/') + "/");
        });

        return services;
    }
}
