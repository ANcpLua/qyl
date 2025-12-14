using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using qyl.mcp.Tools;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton<ITelemetryStore>(InMemoryTelemetryStore.Instance);

var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
jsonOptions.TypeInfoResolverChain.Add(TelemetryJsonContext.Default);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<TelemetryTools>(jsonOptions);

await builder.Build().RunAsync().ConfigureAwait(false);