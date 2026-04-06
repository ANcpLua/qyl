namespace qyl.mcp.Landing;

internal static class LandingPage
{
    private static readonly Lazy<string> CachedHtml = new(ReadEmbeddedHtml);

    public static string GetHtml(string mcpUrl) =>
        CachedHtml.Value.Replace("{{MCP_URL}}", mcpUrl, StringComparison.Ordinal);

    private static string ReadEmbeddedHtml()
    {
        var assembly = typeof(LandingPage).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(static n => n.EndsWithOrdinal("landing.html"));

        if (resourceName is null)
            return FallbackHtml();

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            return FallbackHtml();

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string FallbackHtml() =>
        """
        <!DOCTYPE html>
        <html><head><title>qyl MCP</title></head>
        <body style="background:#0a0a0a;color:#e5e5e5;font-family:monospace;padding:4rem;text-align:center">
        <h1 style="color:#ff6b21">QYL. MCP</h1>
        <p>Observability for AI agents. Connect at <code>/mcp</code></p>
        </body></html>
        """;
}
