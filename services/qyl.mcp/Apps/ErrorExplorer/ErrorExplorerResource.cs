using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace qyl.mcp.Apps.ErrorExplorer;

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
