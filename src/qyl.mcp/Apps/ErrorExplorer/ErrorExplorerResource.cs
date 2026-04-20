namespace qyl.mcp.Apps.ErrorExplorer;

using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

/// <summary>
///     Serves the Error Explorer HTML as an MCP resource at <c>ui://qyl/error-explorer</c>.
///     HTML content is loaded from <c>ErrorExplorerHtml.Content</c>.
/// </summary>
[McpServerResourceType]
public sealed class ErrorExplorerResource
{
    /// <summary>
    ///     Returns the Error Explorer HTML content as an MCP resource.
    /// </summary>
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
