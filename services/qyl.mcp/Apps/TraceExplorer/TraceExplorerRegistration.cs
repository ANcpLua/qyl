using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace qyl.mcp.Apps.TraceExplorer;

internal static class TraceExplorerRegistration
{
    public static IMcpServerBuilder WithTraceExplorer(
        this IMcpServerBuilder builder,
        JsonSerializerOptions jsonOptions) =>
        builder
            .WithTools<TraceExplorerTools>(jsonOptions)
            .WithResources<TraceExplorerResource>();
}
