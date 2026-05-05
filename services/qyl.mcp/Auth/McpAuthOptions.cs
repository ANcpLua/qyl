namespace qyl.mcp.Auth;

public sealed class McpAuthOptions
{
    public const string SectionName = "McpAuth";

    public const string TokenEnvVar = "QYL_MCP_TOKEN";

    public const string HeaderName = "x-mcp-api-key";

    public const string KeycloakAuthorityEnvVar = "QYL_KEYCLOAK_AUTHORITY";

    public const string KeycloakClientIdEnvVar = "QYL_KEYCLOAK_CLIENT_ID";

    public const string KeycloakClientSecretEnvVar = "QYL_KEYCLOAK_CLIENT_SECRET";

    public string? Token { get; set; }

    public bool IsEnabled => !string.IsNullOrWhiteSpace(Token);


    public string? KeycloakAuthority { get; set; }

    public string? KeycloakClientId { get; set; }

    public string? KeycloakClientSecret { get; set; }

    public bool IsKeycloakEnabled =>
        !string.IsNullOrWhiteSpace(KeycloakAuthority) &&
        !string.IsNullOrWhiteSpace(KeycloakClientId) &&
        !string.IsNullOrWhiteSpace(KeycloakClientSecret);
}
