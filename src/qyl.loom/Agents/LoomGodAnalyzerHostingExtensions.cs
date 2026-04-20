namespace Qyl.Loom.Agents;

using Microsoft.AspNetCore.Routing;
using Qyl.Agents.Hosting;

public static class LoomGodAnalyzerHostingExtensions
{
    public static IEndpointRouteBuilder MapLoomGodAnalyzerServer(
        this IEndpointRouteBuilder endpoints,
        LoomGodAnalyzerServer server,
        string pattern = "/mcp/loom") =>
        endpoints.MapMcpServer(server, pattern);

    public static Task RunLoomGodAnalyzerStdioAsync(
        this LoomGodAnalyzerServer server,
        CancellationToken ct = default) =>
        McpHost.RunStdioAsync(server, ct);
}
