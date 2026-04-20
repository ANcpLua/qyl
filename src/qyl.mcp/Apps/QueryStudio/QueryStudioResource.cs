namespace qyl.mcp.Apps.QueryStudio;

using ModelContextProtocol.Server;

/// <summary>
///     MCP resource serving the Query Studio HTML UI at <c>ui://qyl/query-studio</c>.
///     Uses <c>McpServerResource.Create</c> with a delegate for programmatic registration.
/// </summary>
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
