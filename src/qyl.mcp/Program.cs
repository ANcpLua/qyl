using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using qyl.mcp;
using qyl.mcp.Auth;
using qyl.mcp.Tools;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(static o => o.LogToStandardErrorThreshold = LogLevel.Trace);

// Add MCP authentication support (reads QYL_MCP_TOKEN env var)
// If no token configured, auth is disabled (dev mode)
builder.Services.AddMcpAuth(builder.Configuration);

// HTTP client for collector API with resilience (retry, circuit breaker)
// Per CLAUDE.md: qyl.mcp → qyl.collector via HTTP ONLY
var collectorUrl = builder.Configuration["QYL_COLLECTOR_URL"] ?? "http://localhost:5100";

builder.Services.AddCollectorToolClient<ReplayTools>(collectorUrl);
builder.Services.AddCollectorToolClient<HttpTelemetryStore>(collectorUrl);
builder.Services.AddCollectorToolClient<ConsoleTools>(collectorUrl);
builder.Services.AddCollectorToolClient<StructuredLogTools>(collectorUrl);
builder.Services.AddCollectorToolClient<GenAiTools>(collectorUrl);
builder.Services.AddCollectorToolClient<StorageTools>(collectorUrl);
builder.Services.AddCollectorToolClient<CopilotTools>(collectorUrl, TimeSpan.FromSeconds(60));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ITelemetryStore>(static sp =>
    new HttpTelemetryStore(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(HttpTelemetryStore)),
        sp.GetRequiredService<TimeProvider>(),
        sp.GetRequiredService<ILogger<HttpTelemetryStore>>()));

var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
jsonOptions.TypeInfoResolverChain.Add(TelemetryJsonContext.Default);
jsonOptions.TypeInfoResolverChain.Add(ConsoleJsonContext.Default);
jsonOptions.TypeInfoResolverChain.Add(LogsJsonContext.Default);
jsonOptions.TypeInfoResolverChain.Add(GenAiJsonContext.Default);
jsonOptions.TypeInfoResolverChain.Add(StorageJsonContext.Default);
jsonOptions.TypeInfoResolverChain.Add(ReplayJsonContext.Default);
jsonOptions.TypeInfoResolverChain.Add(CopilotJsonContext.Default);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<TelemetryTools>(jsonOptions)
    .WithTools<ReplayTools>(jsonOptions)
    .WithTools<ConsoleTools>(jsonOptions)
    .WithTools<StructuredLogTools>(jsonOptions)
    .WithTools<GenAiTools>(jsonOptions)
    .WithTools<StorageTools>(jsonOptions)
    .WithTools<CopilotTools>(jsonOptions);

await builder.Build().RunAsync().ConfigureAwait(false);
