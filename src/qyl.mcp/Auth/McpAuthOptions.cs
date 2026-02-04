namespace qyl.mcp.Auth;

/// <summary>
///     Authentication options for MCP server communication with qyl.collector.
///     Mirrors the Aspire pattern using x-mcp-api-key header.
/// </summary>
public sealed class McpAuthOptions
{
    /// <summary>
    ///     Configuration section name.
    /// </summary>
    public const string SectionName = "McpAuth";

    /// <summary>
    ///     Environment variable name for the API key.
    /// </summary>
    public const string TokenEnvVar = "QYL_MCP_TOKEN";

    /// <summary>
    ///     HTTP header name for the API key (Aspire pattern).
    /// </summary>
    public const string HeaderName = "x-mcp-api-key";

    /// <summary>
    ///     Gets or sets the API key for authenticating with qyl.collector.
    ///     If null or empty, authentication is disabled (dev mode).
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    ///     Gets whether authentication is enabled.
    /// </summary>
    public bool IsEnabled => !string.IsNullOrWhiteSpace(Token);
}
