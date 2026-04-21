using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace qyl.mcp.Apps.QueryStudio;

internal static class QueryStudioRegistration
{
    public static IMcpServerBuilder WithQueryStudio(
        this IMcpServerBuilder builder,
        JsonSerializerOptions jsonOptions) =>
        builder
            .WithTools<QueryStudioTools>(jsonOptions)
            .WithResources([QueryStudioResource.Create()]);
}
