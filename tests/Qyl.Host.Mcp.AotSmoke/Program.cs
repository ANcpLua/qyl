using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Qyl.Host;
using Qyl.Host.Mcp;

// Live NativeAOT smoke for the public stdio MCP resource path:
// McpHandshakeProbe -> McpClientRegistry -> official MCP SDK source-generated JSON metadata.
// This binary respawns itself with --server and exercises list, call, and resource read.

if (args is ["--server"])
{
    // Nothing may write to stdout except the stdio transport in server mode.
    await using var transport = new StdioServerTransport("aot-smoke");
    await using var stdioServer = McpServer.Create(
        transport,
        SmokeServer.Options(),
        NullLoggerFactory.Instance);
    await stdioServer.RunAsync();
    return 0;
}

var failures = 0;
var app = QylAppBuilder.Create(args);
app.AddMcpStdio("stdio-self", Environment.ProcessPath!, ["--server"]);

var registry = (McpClientRegistry)app.Host.Services
    .Single(static descriptor =>
        descriptor.ServiceType == typeof(McpClientRegistry) &&
        descriptor.ImplementationInstance is not null)
    .ImplementationInstance!;
await using var registryLifetime = registry;

using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));

foreach (var resource in app.Resources)
{
    var state = new QylResourceState
    {
        Name = resource.Name,
        Lifecycle = ResourceLifecycle.Starting,
        Timestamp = DateTimeOffset.UtcNow
    };

    var ready = await resource.ReadinessProbe!.IsReadyAsync(state, timeout.Token);
    Check($"{resource.Name}: initialize + tools/list handshake", ready);
    if (!ready) continue;

    if (!registry.TryGet(resource.Name, out var client))
    {
        Check($"{resource.Name}: client parked in registry", false);
        continue;
    }

    var list = await client.ListToolsAsync(new ListToolsRequestParams(), timeout.Token);
    Check($"{resource.Name}: tools/list returns one echo tool",
        list.Tools.Count == 1 && list.Tools[0].Name == "echo");
    Check($"{resource.Name}: tool list uses SDK JSON metadata",
        McpSdkJson.Serialize(list).Length > 0);

    var callRequest = JsonSerializer.Deserialize(
        """{"name":"echo","arguments":{"value":"live"}}""",
        McpSdkJson.TypeInfo<CallToolRequestParams>())!;
    var call = await client.CallToolAsync(callRequest, timeout.Token);
    Check($"{resource.Name}: tools/call returns echo:live",
        call.Content is [TextContentBlock { Text: "echo:live" }]);
    Check($"{resource.Name}: tool result uses SDK JSON metadata",
        McpSdkJson.Serialize(call).Length > 0);

    var readRequest = JsonSerializer.Deserialize(
        """{"uri":"qyl://resource/static"}""",
        McpSdkJson.TypeInfo<ReadResourceRequestParams>())!;
    var read = await client.ReadResourceAsync(readRequest, timeout.Token);
    Check($"{resource.Name}: resources/read returns resource-body",
        read.Contents is [TextResourceContents { Text: "resource-body" }]);
    Check($"{resource.Name}: resource result uses SDK JSON metadata",
        McpSdkJson.Serialize(read).Length > 0);
}

Console.WriteLine(failures == 0
    ? "AOT MCP live-handshake smoke: PASS"
    : $"AOT MCP live-handshake smoke: {failures} FAILURE(S)");
return failures == 0 ? 0 : 1;

void Check(string label, bool ok)
{
    Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {label}");
    if (!ok) failures++;
}

internal static class SmokeServer
{
    public static McpServerOptions Options() => new()
    {
        ServerInfo = new Implementation { Name = "aot-smoke-server", Version = "1.0.0" },
        ToolCollection = new McpServerPrimitiveCollection<McpServerTool>
        {
            McpServerTool.Create(
                (Func<string, string>)(static value => $"echo:{value}"),
                new McpServerToolCreateOptions
                {
                    Name = "echo",
                    Description = "Echoes a value.",
                    ReadOnly = true
                })
        },
        ResourceCollection = new McpServerResourceCollection
        {
            McpServerResource.Create(
                options: new McpServerResourceCreateOptions
                {
                    UriTemplate = "qyl://resource/static",
                    Name = "static"
                },
                method: (Func<string>)(static () => "resource-body"))
        }
    };
}
