namespace Qyl.Collector.Auth;

/// <summary>
/// Options for the Keycloak OIDC authorization-code + PKCE flow that mints
/// opaque MCP tokens. Wired in <see cref="Hosting.CollectorAuthExtensions"/>.
/// </summary>
/// <remarks>
/// Configuration sources (in precedence order — later wins):
///   1. appsettings.json / appsettings.{Environment}.json under section "Keycloak".
///   2. Environment variables listed in the *EnvVar consts below.
///
/// All fields except <see cref="Authority"/> and <see cref="ClientId"/> have safe
/// defaults; auth flow stays disabled until both are populated.
/// </remarks>
public sealed class KeycloakOptions
{
    public const string SectionName = "Keycloak";

    /// <summary>Base URL of the Keycloak realm — e.g. <c>https://kc.example/realms/qyl</c>.</summary>
    /// <remarks>Discovery doc is fetched from <c>{Authority}/.well-known/openid-configuration</c>.</remarks>
    public const string AuthorityEnvVar = "QYL_KEYCLOAK_AUTHORITY";

    /// <summary>Keycloak server base URL with no realm segment — e.g. <c>https://kc.example</c>. Used to template per-tenant issuers <c>{BaseUrl}/realms/{tenant}</c>.</summary>
    public const string BaseUrlEnvVar = "QYL_KEYCLOAK_BASE_URL";

    /// <summary>Expected <c>aud</c> claim on incoming id_tokens (separate from ClientId in confidential-client flows).</summary>
    public const string AudienceEnvVar = "QYL_KEYCLOAK_AUDIENCE";

    /// <summary>Public/confidential client id registered in Keycloak.</summary>
    public const string ClientIdEnvVar = "QYL_KEYCLOAK_CLIENT_ID";

    /// <summary>Confidential client secret. Empty for public PKCE-only clients.</summary>
    public const string ClientSecretEnvVar = "QYL_KEYCLOAK_CLIENT_SECRET";

    /// <summary>
    /// Comma-separated allowlist of <c>client_redirect_uri</c> values accepted by /auth/authorize.
    /// Empty list disables the allowlist check (use only in dev).
    /// </summary>
    public const string AllowedRedirectsEnvVar = "QYL_OAUTH_ALLOWED_REDIRECTS";

    public string? Authority { get; set; }
    public string? BaseUrl { get; set; }
    public string? Audience { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }

    /// <summary>Allowed <c>client_redirect_uri</c> values. Exact-match.</summary>
    public string[] AllowedRedirects { get; set; } = [];

    /// <summary>How long a /auth/authorize-issued PKCE row stays valid in <c>mcp_pkce_state</c>.</summary>
    public TimeSpan PkceStateTtl { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Cache duration for the OIDC discovery document.</summary>
    public TimeSpan DiscoveryCacheDuration { get; set; } = TimeSpan.FromHours(1);

    public bool IsEnabled => !string.IsNullOrWhiteSpace(Authority) && !string.IsNullOrWhiteSpace(ClientId);
}
