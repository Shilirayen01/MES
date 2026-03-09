// -------------------------------------------------------------------------------------------------
// Program.cs
//
// Entry point for the MES OPC realtime gateway.  This Web application hosts
// a SignalR hub that streams tag values to connected clients.  It configures
// the necessary channels, bus and background publisher to bridge incoming
// tag values from the acquisition layer to the web.  Security and CORS
// policies can be customised via the usual ASP.NET Core middleware.
// -------------------------------------------------------------------------------------------------

using System.Data;
using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Data.SqlClient;
using Mes.Opc.Acquisition.Runtime.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.JsonWebTokens;
using Mes.Opc.Acquisition.Runtime.Persistence;
using Mes.Opc.Acquisition.Runtime.Configuration;
using Mes.Opc.Acquisition.Runtime.Repository;
using Mes.Opc.Acquisition.Runtime.Opc; // Session and subscription managers
using Mes.Opc.Acquisition.Runtime;      // Worker and MachineTagValueWriter
using System.Linq;
using Microsoft.AspNetCore.Authentication;
using Mes.Opc.Platform.Data.Repository;
using Mes.Opc.Platform.Realtime.Services;

namespace Mes.Opc.Platform.Realtime
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // CORS (dev-friendly): allow the UI to call the API + connect to SignalR.
            // For production, replace with a strict WithOrigins("https://your-ui-host") policy.
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    // AllowCredentials is often required for SignalR in browsers.
                    // SetIsOriginAllowed(_) avoids the "*" + credentials restriction.
                    policy.SetIsOriginAllowed(_ => true)
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials();
                });
            });

            // Register SignalR services
            builder.Services.AddSignalR();

            // Configure authentication using JWT bearer tokens.  This setup
            // disables most validation checks so that any bearer token is
            // accepted.  Adjust the TokenValidationParameters to enable
            // issuer, audience and lifetime validation in production.  Without
            // AddAuthentication the [Authorize] attribute on the hub causes
            // ASP.NET Core to throw an InvalidOperationException at runtime.
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateIssuerSigningKey = false,
                    ValidateLifetime = false
                };
                // Allow SignalR connections to send the token as a query string
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        // Extract token from the query string when the request is to the hub
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.HasValue && path.Value.Contains("/opcHub"))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };
            });

            // Configure authorization services so that [Authorize] attributes are honoured.
            builder.Services.AddAuthorization();

            // Register channels for database persistence and realtime streaming.
            // Here we reuse the channel wrappers defined in the acquisition runtime.
            builder.Services.AddSingleton(sp =>
            {
                var channel = Channel.CreateBounded<MachineTagValue>(
                    new BoundedChannelOptions(10_000)
                    {
                        FullMode = BoundedChannelFullMode.Wait
                    });
                return new DbChannel(channel);
            });

            builder.Services.AddSingleton(sp =>
            {
                var channel = Channel.CreateBounded<MachineTagValue>(
                    new BoundedChannelOptions(5_000)
                    {
                        FullMode = BoundedChannelFullMode.DropOldest
                    });
                return new RtChannel(channel);
            });

            // Register the in‑memory tag value bus to fan out values to the DB and RT channels.
            builder.Services.AddSingleton<ITagValueBus, InMemoryTagValueBus>();

            // Register configuration loader and repository for optional API endpoints.
            builder.Services.AddScoped<ConfigLoader>();
            builder.Services.AddScoped<MachineTagValueRepository>();
            builder.Services.AddScoped<UiConfigurationRepository>();

            // --- Dynamic UI realtime routing (per-widget) ---
            // Holds an in-memory map from (MachineCode, OpcNodeId) to widget targets.
            builder.Services.AddSingleton<WidgetBindingCache>();
            // Loads the map from SQL on startup.
            builder.Services.AddHostedService<WidgetBindingCacheLoader>();
            // Dispatches MachineTagValue -> WidgetUpdateDto over SignalR.
            builder.Services.AddSingleton<WidgetValueDispatcher>();

            // Register OPC session and subscription managers.  These managers handle
            // connections to the OPC UA servers and creation of subscriptions.  By
            // registering them here we run the acquisition layer in the same
            // process as the realtime gateway, allowing tag values to flow
            // directly into the shared channels.
            builder.Services.AddSingleton<OpcSessionManager>();
            builder.Services.AddSingleton<OpcSubscriptionManager>();

            // Register the acquisition worker and the database writer as hosted
            // services.  The worker subscribes to configured endpoints and
            // publishes tag values to the bus, while the writer persists them
            // to the database.  Running these hosted services in this process
            // means the realtime hub will receive values without requiring an
            // external message broker.
            builder.Services.AddHostedService<Worker>();
            builder.Services.AddHostedService<MachineTagValueWriter>();

            // Register database connection factory.  Note: this assumes the same connection string
            // configuration keys as the acquisition runtime (DefaultConnection).
            builder.Services.AddScoped<IDbConnection>(sp =>
            {
                var cs = builder.Configuration.GetConnectionString("DefaultConnection");
                return new SqlConnection(cs);
            });

            // Register the background service that publishes tag values to SignalR clients.
            builder.Services.AddHostedService<TagValueSignalRPublisher>();

            var app = builder.Build();


            // Routing + CORS must run before authentication/authorization and endpoints.
            app.UseRouting();
            app.UseCors();

            // AuthN/Z
            app.UseAuthentication();
            app.UseAuthorization();

            // Map the SignalR hub endpoint.  Clients connect to /opcHub and join groups by machine code.
            app.MapHub<OpcTagHub>("/opcHub");

            // Enable serving static files from wwwroot.  UseDefaultFiles
            // enables default document discovery (e.g. index.html).
            app.UseDefaultFiles();
           
            app.UseStaticFiles();

            // Start the web application
            app.Run();
        }
    }
}