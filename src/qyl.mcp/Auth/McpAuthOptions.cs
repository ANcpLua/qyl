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

    // ── Keycloak / OAuth2 client-credentials ────────────────────────────────

    /// <summary>
    ///     Keycloak realm authority URL.
    ///     E.g., "http://localhost:8080/realms/qyl"
    ///     Set via <see cref="KeycloakAuthorityEnvVar"/>.
    ///     When null, Keycloak auth is disabled and <see cref="Token"/> API-key is used.
    /// </summary>
    public string? KeycloakAuthority { get; set; }

    /// <summary>OAuth2 client ID for client-credentials flow.</summary>
    public string? KeycloakClientId { get; set; }

    /// <summary>OAuth2 client secret for client-credentials flow.</summary>
    public string? KeycloakClientSecret { get; set; }

    /// <summary>
    ///     Gets whether Keycloak authentication is fully configured.
    ///     True when <see cref="KeycloakAuthority"/>, <see cref="KeycloakClientId"/>,
    ///     and <see cref="KeycloakClientSecret"/> are all non-empty.
    /// </summary>
    public bool IsKeycloakEnabled =>
        !string.IsNullOrWhiteSpace(KeycloakAuthority) &&
        !string.IsNullOrWhiteSpace(KeycloakClientId) &&
        !string.IsNullOrWhiteSpace(KeycloakClientSecret);

    public const string KeycloakAuthorityEnvVar     = "QYL_KEYCLOAK_AUTHORITY";
    public const string KeycloakClientIdEnvVar       = "QYL_KEYCLOAK_CLIENT_ID";
    public const string KeycloakClientSecretEnvVar   = "QYL_KEYCLOAK_CLIENT_SECRET";
}
