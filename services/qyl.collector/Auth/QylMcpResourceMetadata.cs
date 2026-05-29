using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Authentication;

namespace Qyl.Collector.Auth;

internal static class QylMcpResourceMetadata
{
    // RFC 9728 Protected Resource Metadata, populated per request. The SDK's metadata route
    // (.well-known/oauth-protected-resource/mcp/{tenant}) doesn't expose {tenant} as a named route
    // value, so the tenant is derived from the resource path. The Keycloak base URL is resolved at
    // request time (not registration time) so it reflects live configuration. Keycloak's issuer is
    // deterministic ({baseUrl}/realms/{tenant}), so authorization_servers is templated without a
    // discovery fetch — keeping this unauthenticated endpoint side-effect-free.
    public static Task PopulateAsync(ResourceMetadataRequestContext context)
    {
        var request = context.HttpContext.Request;
        var tenant = ExtractTenant(request.Path.Value);
        if (string.IsNullOrEmpty(tenant))
            return Task.CompletedTask;

        var config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var baseUrl = ResolveBaseUrl(config[KeycloakOptions.BaseUrlEnvVar], config[KeycloakOptions.AuthorityEnvVar]);
        if (baseUrl is null)
            return Task.CompletedTask;

        context.ResourceMetadata = new ProtectedResourceMetadata
        {
            Resource = $"{request.Scheme}://{request.Host}/mcp/{tenant}",
            AuthorizationServers = [$"{baseUrl}/realms/{tenant}"],
            BearerMethodsSupported = ["header"],
            ScopesSupported = ["qyl.collector"]
        };
        return Task.CompletedTask;
    }

    private static string? ExtractTenant(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return null;
        const string marker = "/mcp/";
        var index = path.IndexOf(marker, StringComparison.Ordinal);
        return index < 0 ? null : path[(index + marker.Length)..].Trim('/');
    }

    // Keycloak base URL with no realm segment. Prefers an explicit BaseUrl; else derives it from Authority.
    public static string? ResolveBaseUrl(string? baseUrl, string? authority)
    {
        if (!string.IsNullOrWhiteSpace(baseUrl))
            return baseUrl.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(authority))
            return null;

        var trimmed = authority.TrimEnd('/');
        var marker = trimmed.LastIndexOf("/realms/", StringComparison.Ordinal);
        return marker > 0 ? trimmed[..marker] : trimmed;
    }
}
