using Qyl.Collector;
using Qyl.Collector.Hosting;
using Qyl.Instrumentation.Instrumentation;

Console.WriteLine($"[qyl] Process starting at {TimeProvider.System.GetUtcNow():O}");

var builder = WebApplication.CreateSlimBuilder(args);

builder.AddQylServiceDefaults(options =>
{
    // Collector is an OTLP backend — disable OpenAPI (we ship our own generated openapi.yaml)
    // and auto-discovery (collector never phones out to another collector).
    options.EnableOpenApi = false;
    options.EnableAutoDiscovery = false;
    options.AdditionalActivitySources.Add("Qyl.Collector");
});

var ports = builder.Services.AddQylCollectorCore(builder.Configuration);
builder.Services.AddQylCollectorStorage(builder.Configuration);
builder.Services.AddQylCollectorAuth(builder.Configuration, builder.Environment);
builder.Services.AddQylCollectorTelemetry(builder.Environment);
builder.Services.AddQylCollectorFeatures(builder.Configuration);
builder.WebHost.ConfigureQylCollectorKestrel(builder.Configuration);

var app = builder.Build();

await app.InitializeQylCollectorAsync().ConfigureAwait(false);
app.UseQylCollectorMiddleware();
app.MapQylCollectorEndpoints();

StartupBanner.Print(
    $"http://localhost:{ports.Http}", ports.Http, ports.Grpc, ports.OtlpHttp,
    app.Services.GetRequiredService<OtlpCorsOptions>(),
    app.Services.GetRequiredService<OtlpApiKeyOptions>());

app.Lifetime.ApplicationStarted.Register(() =>
    Console.WriteLine($"[qyl] Application started and listening on port {ports.Http}"));

await app.RunAsync().ConfigureAwait(false);
