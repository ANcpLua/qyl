using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace qyl.mcp.Apps.TraceExplorer;

/// <summary>
///     Registers Trace Explorer tools and resources on an MCP server builder.
/// </summary>
internal static class TraceExplorerRegistration
{
    public static IMcpServerBuilder WithTraceExplorer(
        this IMcpServerBuilder builder,
        JsonSerializerOptions jsonOptions) =>
        builder
            .WithTools<TraceExplorerTools>(jsonOptions)
            .WithResources<TraceExplorerResource>();
}
