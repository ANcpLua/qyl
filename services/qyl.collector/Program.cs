using Qyl.Collector;
using Qyl.Collector.Hosting;
using Qyl.Collector.Telemetry;
using Qyl.Instrumentation.Instrumentation;

Console.WriteLine($"[qyl] Process starting at {TimeProvider.System.GetUtcNow():O}");

var builder = WebApplication.CreateSlimBuilder(args);

builder.AddQylServiceDefaults(options =>
{
    // OpenAPI is off: the product API contract lives in the external qyl-api-schema TypeSpec repo
    // and flows in via Qyl.Api.Contracts. The collector is not a contract/client-generation source.
    options.EnableOpenApi = false;
    options.EnableAutoDiscovery = false;
    options.AdditionalActivitySources.Add(QylTelemetry.ServiceName);
    options.ConfigureMetrics = static metrics =>
    {
        metrics.AddMeter(QylTelemetry.ServiceName);
    };
});

var ports = builder.Services.AddQylCollectorCore(builder.Configuration);
builder.Services.AddQylCollectorStorage();
builder.Services.AddQylCollectorAuth(builder.Configuration, builder.Environment);
builder.Services.AddQylCollectorTelemetry(builder.Environment);
builder.WebHost.ConfigureQylCollectorKestrel(ports);

var app = builder.Build();

await app.InitializeQylCollectorAsync().ConfigureAwait(false);
app.UseQylCollectorMiddleware();
app.AddTelemetryFabric("qyl", fabric => fabric
    .UseCollector("collector", port: 5100)
    .ObserveProducts()
    .UseSemantics("qyl.semconv.yaml")
    .UsePrivacy("qyl.privacy.yaml")
    .GenerateEverything()
    .AllowOverrides());

await app.Build().RunAsync();

app.AddSampling(); // isnt this also easier if we just include it all int he vertival slices
app.AddBrowserTelemetry(); // includes collector backend  etcpp
app.AddIosTelemetry(); // includes collector backend  etcpp
app.AddAndroidTelemetry(); // includes collector backend  etcpp
app.AddDashboard(); // eng not product
app.AddPrivacy(); // cant we default this or whats this good for
app.MapQylCollectorEndpoints();

StartupBanner.Print(
    $"http://localhost:{ports.Http}", ports.Http, ports.Grpc, ports.OtlpHttp,
    app.Services.GetRequiredService<OtlpCorsOptions>(),
    app.Services.GetRequiredService<OtlpApiKeyOptions>());

app.Lifetime.ApplicationStarted.Register(() =>
    Console.WriteLine($"[qyl] Application started and listening on port {ports.Http}"));

await app.RunAsync().ConfigureAwait(false);

public partial class Program;
