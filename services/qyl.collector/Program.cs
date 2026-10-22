using Microsoft.Extensions.Options;
using Qyl.Collector;
using Qyl.Collector.ApiKeys;
using Qyl.Collector.Hosting;
using Qyl.Collector.Telemetry;
using Qyl.Instrumentation.Instrumentation;

Console.WriteLine($"[qyl] Process starting at {TimeProvider.System.GetUtcNow():O}");

var builder = WebApplication.CreateSlimBuilder(args);

builder.UseQyl(options =>
{
    // OpenAPI is off: the product API contract is the TypeSpec in contracts/ and flows in via
    // Qyl.Api.Contracts. The collector is not a contract/client-generation source.
    options.EnableOpenApi = false;
    options.EnableAutoDiscovery = false;
    options.AdditionalMeters.Add(QylTelemetry.ServiceName);
});

builder.Services.AddQylCollectorCore(builder.Configuration);
builder.Services.AddQylCollectorStorage(builder.Configuration);
builder.Services.AddQylCollectorAuth(builder.Configuration, builder.Environment);
builder.Services.AddQylCollectorTelemetry(builder.Environment);
builder.Services.ConfigureQylCollectorKestrel();

var app = builder.Build();
var ports = app.Services.GetRequiredService<IOptions<CollectorPortOptions>>().Value;
CollectorSelfExportGuard.ThrowIfSelfExporting(app.Configuration, ports);

app.UseQylCollectorMiddleware();

app.MapQylCollectorEndpoints();
CollectorHealthGuard.ThrowIfHealthSurfaceUnwired(app);

StartupBanner.Print(
    ports,
    app.Services.GetRequiredService<IOptions<OtlpCorsOptions>>().Value,
    app.Services.GetRequiredService<IOptions<OtlpApiKeyOptions>>().Value);

app.Lifetime.ApplicationStarted.Register(() =>
    Console.WriteLine($"[qyl] Application started and listening on port {ports.Http}"));

await app.RunAsync().ConfigureAwait(false);

