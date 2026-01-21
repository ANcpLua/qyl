using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using qyl.mcp.Tools;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

// HTTP client for collector API with resilience (retry, circuit breaker)
// Per CLAUDE.md: qyl.mcp → qyl.collector via HTTP ONLY
var collectorUrl = builder.Configuration["QYL_COLLECTOR_URL"] ?? "http://localhost:5100";

builder.Services.AddHttpClient<ReplayTools>(client =>
    {
        client.BaseAddress = new Uri(collectorUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddExtendedHttpClientLogging()
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<HttpTelemetryStore>(client =>
    {
        client.BaseAddress = new Uri(collectorUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddExtendedHttpClientLogging()
    .AddStandardResilienceHandler();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ITelemetryStore>(static sp =>
    new HttpTelemetryStore(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(HttpTelemetryStore)),
        sp.GetRequiredService<TimeProvider>(),
        sp.GetRequiredService<ILogger<HttpTelemetryStore>>()));

var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
jsonOptions.TypeInfoResolverChain.Add(TelemetryJsonContext.Default);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<TelemetryTools>(jsonOptions)
    .WithTools<ReplayTools>(jsonOptions);

await builder.Build().RunAsync().ConfigureAwait(false);
