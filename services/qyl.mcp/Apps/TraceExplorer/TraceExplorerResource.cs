using System.Reflection;
using ModelContextProtocol.Server;

namespace qyl.mcp.Apps.TraceExplorer;

/// <summary>
///     MCP resource serving the Trace Explorer HTML at <c>ui://qyl/trace-viewer</c>.
///     Loads from embedded resource first, then falls back to file on disk.
/// </summary>
[McpServerResourceType]
internal sealed class TraceExplorerResource
{
    private static readonly Lazy<string> s_html = new(LoadHtml);

    [McpServerResource(UriTemplate = "ui://qyl/trace-viewer",
        Name = "Trace Explorer",
        MimeType = "text/html;profile=mcp-app")]
    public static string GetTraceViewer() => s_html.Value;

    private static string LoadHtml()
    {
        const string embeddedName = "qyl.mcp.Apps.TraceExplorer.trace-viewer.html";
        using var stream = typeof(TraceExplorerResource).Assembly
            .GetManifestResourceStream(embeddedName);

        if (stream is not null)
        {
            using StreamReader reader = new(stream);
            return reader.ReadToEnd();
        }

        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
        var filePath = Path.Combine(assemblyDir, "Apps", "TraceExplorer", "trace-viewer.html");

        return File.Exists(filePath)
            ? File.ReadAllText(filePath)
            : "<!-- trace-viewer.html not found -->";
    }
}
