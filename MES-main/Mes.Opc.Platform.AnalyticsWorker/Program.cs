using Mes.Opc.Platform.AnalyticsWorker;
using Mes.Opc.Platform.AnalyticsWorker.Data;
using Mes.Opc.Platform.AnalyticsWorker.Engine;
using Mes.Opc.Platform.AnalyticsWorker.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((ctx, services) =>
    {
        services.Configure<AnalyticsWorkerOptions>(ctx.Configuration.GetSection("AnalyticsWorker"));

        services.AddSingleton<SqlConnectionFactory>();
        services.AddSingleton<ProductionRunRepository>();
        services.AddSingleton<ConfigRepository>();
        services.AddSingleton<ResultRepository>();
        services.AddSingleton<TagValueRepository>();

        services.AddSingleton<TagAggregationEngine>();
        services.AddSingleton<KpiEngine>();
        services.AddSingleton<SummaryEngine>();
        services.AddSingleton<QualityEngine>();

        services.AddHostedService<AnalyticsWorker>();
    })
    .Build();

await host.RunAsync();
