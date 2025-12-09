using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using qyl.mcp.server.Tools;

var builder = Host.CreateApplicationBuilder(args);

// Configure all logs to go to stderr (stdout is used for the MCP protocol messages).
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

// Register telemetry store (replace with your actual implementation)
builder.Services.AddSingleton<ITelemetryStore>(InMemoryTelemetryStore.Instance);

// Create JSON options with our telemetry types for AOT compatibility
var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
jsonOptions.TypeInfoResolverChain.Add(TelemetryJsonContext.Default);

// Add the MCP services: the transport to use (stdio) and the tools to register.
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<TelemetryTools>(jsonOptions);

await builder.Build().RunAsync().ConfigureAwait(false);