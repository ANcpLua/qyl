using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using qyl.mcp.Tools;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton<ITelemetryStore>(InMemoryTelemetryStore.Instance);

// HTTP client for collector API with resilience (retry, circuit breaker)
var collectorUrl = builder.Configuration["QYL_COLLECTOR_URL"] ?? "http://localhost:5100";
builder.Services.AddHttpClient<ReplayTools>(client =>
{
    client.BaseAddress = new Uri(collectorUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddExtendedHttpClientLogging()
.AddStandardResilienceHandler();

var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
jsonOptions.TypeInfoResolverChain.Add(TelemetryJsonContext.Default);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<TelemetryTools>(jsonOptions)
    .WithTools<ReplayTools>(jsonOptions);

await builder.Build().RunAsync().ConfigureAwait(false);
