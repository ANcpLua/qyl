using ModelContextProtocol.Server;

namespace qyl.mcp.Apps.QueryStudio;

internal static class QueryStudioResource
{
    public static McpServerResource Create() =>
        McpServerResource.Create(
            static () => QueryStudioHtml.Content,
            new McpServerResourceCreateOptions
            {
                Name = "query-studio",
                Description = "Interactive DuckDB query console for qyl observability data",
                MimeType = "text/html;profile=mcp-app"
            });
}
