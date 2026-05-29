using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Qyl.Collector.Auth;

/// <summary>
/// The single supported tenancy model: one collector serves many tenants on one host. The
/// <c>{tenant}</c> route segment selects the tenant namespace (addressing only); the
/// token-bound <c>qyl.tenant_id</c> claim is the authorization authority. The requirement is
/// satisfied only when the route tenant equals the token tenant — otherwise the request is
/// denied with 403 Forbidden.
/// </summary>
internal sealed class McpTenantMatchRequirement : IAuthorizationRequirement;

internal sealed partial class McpTenantMatchAuthorizationHandler(
    IOptions<KeycloakOptions> options,
    ILogger<McpTenantMatchAuthorizationHandler> logger)
    : AuthorizationHandler<McpTenantMatchRequirement>
{
    private const string TenantRouteKey = "tenant";

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, McpTenantMatchRequirement requirement)
    {
        if (context.Resource is not HttpContext http)
            return Task.CompletedTask;

        var routeTenant = http.Request.RouteValues.TryGetValue(TenantRouteKey, out var raw)
            ? raw as string
            : null;

        var tenantClaim = options.Value.TenantClaim;
        var tokenTenant = context.User.FindFirst(tenantClaim)?.Value;

        if (!string.IsNullOrEmpty(tokenTenant)
            && !string.IsNullOrEmpty(routeTenant)
            && string.Equals(routeTenant, tokenTenant, StringComparison.Ordinal))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        LogTenantDenied(tokenTenant ?? "(none)", routeTenant ?? "(none)", tenantClaim);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "MCP tenant authorization denied — token tenant '{TokenTenant}' from claim '{TenantClaim}' cannot access route tenant '{RouteTenant}'")]
    private partial void LogTenantDenied(string tokenTenant, string routeTenant, string tenantClaim);
}
