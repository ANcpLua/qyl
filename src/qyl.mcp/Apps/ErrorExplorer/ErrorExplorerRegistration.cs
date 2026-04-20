namespace qyl.mcp.Apps.ErrorExplorer;

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

internal static class ErrorExplorerRegistration
{
    public static IMcpServerBuilder WithErrorExplorer(
        this IMcpServerBuilder builder,
        JsonSerializerOptions jsonOptions) =>
        builder
            .WithTools<ErrorExplorerTools>(jsonOptions)
            .WithResources<ErrorExplorerResource>();
}
