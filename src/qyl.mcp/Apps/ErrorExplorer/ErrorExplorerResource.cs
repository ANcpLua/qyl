using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace qyl.mcp.Apps.ErrorExplorer;

/// <summary>
///     Serves the Error Explorer HTML as an MCP resource at <c>ui://qyl/error-explorer</c>.
///     HTML content is loaded from <c>ErrorExplorerHtml.Content</c>.
/// </summary>
[McpServerResourceType]
public sealed class ErrorExplorerResource
{
    [McpServerResource(
        UriTemplate = "ui://qyl/error-explorer",
        Name = "error-explorer",
        Title = "Error Explorer",
        MimeType = "text/html;profile=mcp-app")]
    public static ReadResourceResult Get() =>
        new()
        {
            Contents =
            [
                new TextResourceContents
                {
                    Uri = "ui://qyl/error-explorer",
                    MimeType = "text/html;profile=mcp-app",
                    Text = ErrorExplorerHtml.Content
                }
            ]
        };
}
