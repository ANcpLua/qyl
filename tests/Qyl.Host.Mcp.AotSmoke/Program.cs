using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Qyl.Host;
using Qyl.Host.Mcp;

// AOT smoke for Qyl.Host.Mcp: not just composition — a LIVE MCP handshake under Native AOT.
// Two resource kinds are exercised end-to-end through the package's own machinery
// (McpHandshakeProbe -> McpClientRegistry -> official MCP SDK source-generated JSON metadata):
//   inproc      — SDK server over an in-memory stream pair inside this process
//   stdio-self  — this same native binary re-spawned with --server as a stdio MCP server
// Exit code 0 = every step passed.

if (args is ["--server"])
{
    // Stdio server half. Nothing may write to stdout except the transport.
    await using var serverServices = new ServiceCollection().BuildServiceProvider();
#pragma warning disable CA2000 // ownership transfers to the McpServer (same pattern as QylMcpBuilderExtensions)
    await using var stdioServer = McpServer.Create(
        new StdioServerTransport("aot-smoke"), SmokeServer.Options(), NullLoggerFactory.Instance, serverServices);
#pragma warning restore CA2000
    await stdioServer.RunAsync();
    return 0;
}

var failures = 0;

await using var services = new ServiceCollection().BuildServiceProvider();
var app = QylAppBuilder.Create(args);
app.AddMcpInProcess("inproc", transport =>
    McpServer.Create(transport, SmokeServer.Options(), NullLoggerFactory.Instance, services));
app.AddMcpStdio("stdio-self", Environment.ProcessPath!, ["--server"]);

var registry = (McpClientRegistry)app.Host.Services
    .Single(static d => d.ServiceType == typeof(McpClientRegistry) && d.ImplementationInstance is not null)
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
    Check($"{resource.Name}: tools/list returns official protocol result (1 tool 'echo')",
        list.Tools.Count == 1 && list.Tools[0].Name == "echo");
    Check($"{resource.Name}: official list serializes via SDK source-gen metadata",
        McpSdkJson.Serialize(list).Length > 0);

    var callRequest = JsonSerializer.Deserialize(
        """{"name":"echo","arguments":{"value":"live"}}""",
        McpSdkJson.TypeInfo<CallToolRequestParams>())!;
    var call = await client.CallToolAsync(callRequest, timeout.Token);
    Check($"{resource.Name}: tools/call echo -> 'echo:live'",
        call.Content is [TextContentBlock { Text: "echo:live" }]);
    Check($"{resource.Name}: official call result serializes via SDK source-gen metadata",
        McpSdkJson.Serialize(call).Length > 0);

    var readRequest = JsonSerializer.Deserialize(
        """{"uri":"qyl://resource/static"}""",
        McpSdkJson.TypeInfo<ReadResourceRequestParams>())!;
    var read = await client.ReadResourceAsync(readRequest, timeout.Token);
    Check($"{resource.Name}: resources/read -> 'resource-body'",
        read.Contents is [TextResourceContents { Text: "resource-body" }]);
    Check($"{resource.Name}: official resource result serializes via SDK source-gen metadata",
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
