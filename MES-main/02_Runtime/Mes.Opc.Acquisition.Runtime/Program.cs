// -------------------------------------------------------------------------------------------------
// Program.cs
//
// Entry point of the MES OPC runtime service.  This program configures dependency
// injection, registers all services needed by the runtime and starts the generic
// host.  By isolating the acquisition layer in its own process the solution
// can scale independently from any Web or UI layer.  Comments throughout the
// file explain the purpose of each registration and how the host is constructed.
// -------------------------------------------------------------------------------------------------

using System;                // Provides the Host builder and hosted service infrastructure
using System.Data;                                 // Provides IDbConnection for database access
using System.Threading.Channels;                   // Provides channel types for producer/consumer patterns
using Mes.Opc.Acquisition.Runtime;                 // Imports Worker and related types defined in this project
using Mes.Opc.Acquisition.Runtime.Configuration;  // Imports configuration loader definitions
using Mes.Opc.Acquisition.Runtime.Cycle;
using Mes.Opc.Acquisition.Runtime.Infrastructure;  // Imports infrastructure services such as channels and bus
using Mes.Opc.Acquisition.Runtime.Opc;             // Imports OPC session and subscription managers
using Mes.Opc.Acquisition.Runtime.Persistence;     // Imports the MachineTagValue model
using Mes.Opc.Acquisition.Runtime.Repository;      // Imports the repository for persisting MachineTagValue entities
using Microsoft.Data.SqlClient;                   // Provides SqlConnection for SQL Server access
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;     // Provides extension methods for registering services
using Microsoft.Extensions.Hosting;

// Build a host for the runtime service.  The Host encapsulates configuration,
// dependency injection, logging and hosted service management.  It takes
// command line arguments which can be used to override configuration values.
var builder = Host.CreateApplicationBuilder(args);

// Register a scoped factory for IDbConnection.  Each time a component
// requires an IDbConnection, the dependency injection container will call
// this lambda to create a new SqlConnection configured with the connection
// string named "DefaultConnection" from appsettings.json.  Keeping the
// connection scoped ensures that the same connection is reused within a
// single scope.
builder.Services.AddScoped<IDbConnection>(sp =>
{
    var cs = builder.Configuration.GetConnectionString("DefaultConnection");
    return new SqlConnection(cs);
});

// Register configuration loader and repository as scoped services.  They
// encapsulate reading configuration from the database and persisting
// MachineTagValue entities.
builder.Services.AddScoped<ConfigLoader>();
builder.Services.AddScoped<MachineTagValueRepository>();
// Cycle tracking (ProductRunTracker) - fully optional, enabled by configuration.
builder.Services.Configure<CycleTrackingOptions>(builder.Configuration.GetSection("CycleTracking"));
builder.Services.AddSingleton<CycleRuleStore>();
builder.Services.AddSingleton<ICycleRuleProvider>(sp => sp.GetRequiredService<CycleRuleStore>());
builder.Services.AddSingleton<CycleRuleRepository>();
builder.Services.AddSingleton<IScopeResolver>(sp =>
{
    var opt = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CycleTrackingOptions>>().Value;
    return string.Equals(opt.ScopeResolver, "Db311", StringComparison.OrdinalIgnoreCase)
        ? new Db311ScopeResolver()
        : new Db311ScopeResolver();
});
builder.Services.AddSingleton<IProductRunTracker, ProductRunTracker>();
builder.Services.AddScoped<IProductionRunRepository, ProductionRunRepository>();
builder.Services.AddHostedService<CycleRuleReloadService>();

// Register OPC session and subscription managers as singletons.  These types
// manage the low‑level interactions with the OPC UA server such as
// establishing sessions and creating subscriptions.
builder.Services.AddSingleton<OpcSessionManager>();

builder.Services.Configure<OpcSubscriptionOptions>(
    builder.Configuration.GetSection("OpcSubscription"));

builder.Services.AddSingleton<OpcSubscriptionManager>();

// Register the main worker as a hosted service.  The worker orchestrates
// endpoint runners, reloads configuration periodically and keeps the runners
// alive until the host shuts down.
builder.Services.AddHostedService<Worker>();

// -------------------------------------------------------------------------
// Channel registrations
//
// Two separate channels are registered for different consumers: one for
// persisting values to the database and one for real‑time streaming.  Each
// channel uses a bounded capacity and a specific overflow policy.  Wrapper
// types (DbChannel and RtChannel) are used so that the DI container can
// distinguish between them.

// Database channel: use FullMode.Wait to exert backpressure on producers.  A
// capacity of 10k messages should be sufficient for moderate load.  If the
// buffer fills up the writer will block producers rather than drop data.
builder.Services.AddSingleton(sp =>
{
    var channel = Channel.CreateBounded<MachineTagValue>(
        new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
    return new DbChannel(channel);
});

// Real‑time channel: use FullMode.DropOldest so that the UI never blocks the
// acquisition system.  A smaller capacity (5k) is sufficient because only
// the latest values need to be delivered to connected clients.
builder.Services.AddSingleton(sp =>
{
    var channel = Channel.CreateBounded<MachineTagValue>(
        new BoundedChannelOptions(5_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
    return new RtChannel(channel);
});

// Register the tag value bus.  The bus fans out values to both the DB
// and real‑time channels.  Consumers publish values to the bus instead of
// writing directly to channels.  The bus logs when values are dropped due
// to full buffers.  Because we import the Infrastructure namespace above
// we can refer to the types without qualification.
builder.Services.AddSingleton<ITagValueBus, InMemoryTagValueBus>();

// Register the MachineTagValueWriter as a hosted service.  It consumes
// values from the DB channel and writes them to the database in batches.  The
// writer depends on DbChannel, which resolves the correct channel instance.
builder.Services.AddHostedService<MachineTagValueWriter>();

// Build the host with the configured services and settings.  This prepares
// the dependency injection container and other infrastructure but does not
// start any services yet.
var host = builder.Build();

// Run the host.  This starts all hosted services and blocks until the
// application is shut down.



Console.WriteLine("==== CONFIG DUMP ====");
Console.WriteLine("ENV = " + builder.Environment.EnvironmentName);

var cfg = builder.Configuration;
Console.WriteLine("CycleTracking:Enabled = " + cfg["CycleTracking:Enabled"]);
Console.WriteLine("CycleTracking:RuleReloadSeconds = " + cfg["CycleTracking:RuleReloadSeconds"]);
Console.WriteLine("CycleTracking:ScopeResolver = " + cfg["CycleTracking:ScopeResolver"]);

var opt = host.Services
    .GetRequiredService<Microsoft.Extensions.Options.IOptions<CycleTrackingOptions>>()
    .Value;

Console.WriteLine($"Options.Enabled = {opt.Enabled}");

host.Run();