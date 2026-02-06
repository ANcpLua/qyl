using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using qyl.mcp.Auth;
using qyl.mcp.Tools;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

// Add MCP authentication support (reads QYL_MCP_TOKEN env var)
// If no token configured, auth is disabled (dev mode)
builder.Services.AddMcpAuth(builder.Configuration);

// HTTP client for collector API with resilience (retry, circuit breaker)
// Per CLAUDE.md: qyl.mcp → qyl.collector via HTTP ONLY
var collectorUrl = builder.Configuration["QYL_COLLECTOR_URL"] ?? "http://localhost:5100";

// Configure HttpClient for each tool class that needs collector access
builder.Services.AddHttpClient<ReplayTools>(client =>
    {
        client.BaseAddress = new Uri(collectorUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddMcpAuthHandler()
    .AddExtendedHttpClientLogging()
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<HttpTelemetryStore>(client =>
    {
        client.BaseAddress = new Uri(collectorUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddMcpAuthHandler()
    .AddExtendedHttpClientLogging()
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<ConsoleTools>(client =>
    {
        client.BaseAddress = new Uri(collectorUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddMcpAuthHandler()
    .AddExtendedHttpClientLogging()
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<StructuredLogTools>(client =>
    {
        client.BaseAddress = new Uri(collectorUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddMcpAuthHandler()
    .AddExtendedHttpClientLogging()
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<GenAiTools>(client =>
    {
        client.BaseAddress = new Uri(collectorUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddMcpAuthHandler()
    .AddExtendedHttpClientLogging()
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<StorageTools>(client =>
    {
        client.BaseAddress = new Uri(collectorUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddMcpAuthHandler()
    .AddExtendedHttpClientLogging()
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<CopilotTools>(client =>
    {
        client.BaseAddress = new Uri(collectorUrl);
        client.Timeout = TimeSpan.FromSeconds(60); // Copilot chat can take longer
    })
    .AddMcpAuthHandler()
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
