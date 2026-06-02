namespace Qyl.Collector.Auth;

/// <summary>
/// Options for validating Keycloak-issued JWT bearer tokens.
/// </summary>
/// <remarks>
/// Populated from the environment variables named by the <c>*EnvVar</c> constants below.
/// </remarks>
public sealed class KeycloakOptions
{
    /// <summary>Base URL of the Keycloak realm — e.g. <c>https://kc.example/realms/qyl</c>.</summary>
    /// <remarks>Discovery doc is fetched from <c>{Authority}/.well-known/openid-configuration</c>.</remarks>
    public const string AuthorityEnvVar = "QYL_KEYCLOAK_AUTHORITY";

    /// <summary>Keycloak server base URL with no realm segment — e.g. <c>https://kc.example</c>. Used to template per-tenant issuers <c>{BaseUrl}/realms/{tenant}</c>.</summary>
    public const string BaseUrlEnvVar = "QYL_KEYCLOAK_BASE_URL";

    /// <summary>Expected <c>aud</c> claim on incoming access tokens.</summary>
    public const string AudienceEnvVar = "QYL_KEYCLOAK_AUDIENCE";

    /// <summary>JWT claim that binds a token to a qyl tenant.</summary>
    public const string TenantClaimEnvVar = "QYL_KEYCLOAK_TENANT_CLAIM";

    public string? Authority { get; set; }
    public Uri? BaseUrl { get; set; }
    public string? Audience { get; set; }
    public string TenantClaim { get; set; } = "qyl.tenant_id";

    public bool IsEnabled => !string.IsNullOrWhiteSpace(Authority) && !string.IsNullOrWhiteSpace(Audience);
}
