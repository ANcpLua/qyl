using OpenTelemetry.Metrics;
using Qyl.Collector;
using Qyl.Collector.Hosting;
using Qyl.Collector.Telemetry;
using Qyl.Instrumentation.Instrumentation;

Console.WriteLine($"[qyl] Process starting at {TimeProvider.System.GetUtcNow():O}");

var builder = WebApplication.CreateSlimBuilder(args);

builder.AddQylServiceDefaults(options =>
{
    options.EnableOpenApi = false;
    options.EnableAutoDiscovery = false;
    options.AdditionalActivitySources.Add(QylTelemetry.ServiceName);
    options.ConfigureMetrics = static metrics =>
    {
        metrics.AddMeter(QylTelemetry.ServiceName);
        metrics.AddMeter(QylTelemetry.StorageMeterName);
        metrics.AddMeter(QylTelemetry.ConversationsMeterName);
    };
});

var ports = builder.Services.AddQylCollectorCore(builder.Configuration);
builder.Services.AddQylCollectorStorage();
builder.Services.AddQylCollectorAuth(builder.Configuration, builder.Environment);
builder.Services.AddQylCollectorTelemetry(builder.Environment);
builder.Services.AddQylCollectorFeatures(builder.Configuration);
builder.WebHost.ConfigureQylCollectorKestrel(builder.Configuration);

// Multi-tenant MCP endpoint (/mcp/{tenant}) + Keycloak JWT bearer auth + RFC 9728 PRM.
// Services register unconditionally; the endpoint and auth middleware are flag-gated below.
builder.Services.AddQylCollectorMcp(builder.Configuration);
builder.Services.AddQylMcpAuthentication(builder.Configuration, builder.Environment);

var app = builder.Build();

await app.InitializeQylCollectorAsync().ConfigureAwait(false);
app.UseQylCollectorMiddleware();

var mcpTenantAuthEnabled = CollectorAuthExtensions.IsMcpTenantAuthEnabled(app.Configuration);
if (mcpTenantAuthEnabled)
{
    CollectorAuthExtensions.EnsureMcpTenantAuthConfiguration(app.Configuration);
    app.UseAuthentication();
    app.UseAuthorization();
}

app.MapQylCollectorEndpoints();

if (mcpTenantAuthEnabled)
    app.MapMcp("/mcp/{tenant}").RequireAuthorization(CollectorAuthExtensions.McpTenantPolicy);

StartupBanner.Print(
    $"http://localhost:{ports.Http}", ports.Http, ports.Grpc, ports.OtlpHttp,
    app.Services.GetRequiredService<OtlpCorsOptions>(),
    app.Services.GetRequiredService<OtlpApiKeyOptions>());

app.Lifetime.ApplicationStarted.Register(() =>
    Console.WriteLine($"[qyl] Application started and listening on port {ports.Http}"));

await app.RunAsync().ConfigureAwait(false);

public partial class Program;
