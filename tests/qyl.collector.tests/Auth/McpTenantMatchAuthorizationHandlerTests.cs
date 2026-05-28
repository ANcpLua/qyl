using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Qyl.Collector.Auth;

namespace Qyl.Collector.Tests.Auth;

/// <summary>
/// Unit coverage for the sole tenancy authority rule: the route {tenant} is namespace/addressing
/// only; authorization derives from the token-bound qyl.tenant_id claim. Route == token -> allow,
/// otherwise deny (which surfaces as 403 at the endpoint).
/// </summary>
public sealed class McpTenantMatchAuthorizationHandlerTests
{
    private static async Task<bool> EvaluateAsync(string routeTenant, string? tokenTenant)
    {
        var http = new DefaultHttpContext();
        http.Request.RouteValues["tenant"] = routeTenant;

        Claim[] claims = tokenTenant is null
            ? []
            : [new Claim(BearerOpaqueTokenAuthenticationHandler.TenantClaimType, tokenTenant)];
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "BearerOpaque"));

        var requirement = new McpTenantMatchRequirement();
        var context = new AuthorizationHandlerContext([requirement], user, http);

        await new McpTenantMatchAuthorizationHandler(
            NullLogger<McpTenantMatchAuthorizationHandler>.Instance).HandleAsync(context);

        return context.HasSucceeded;
    }

    [Fact]
    public async Task Succeeds_WhenRouteTenantEqualsTokenTenant()
    {
        var allowed = await EvaluateAsync(routeTenant: "acme", tokenTenant: "acme");
        allowed.Should().BeTrue();
    }

    [Fact]
    public async Task Denies_WhenRouteTenantDiffersFromTokenTenant()
    {
        // The route claims "globex" but the token authority is "acme" — the route is never trusted.
        var allowed = await EvaluateAsync(routeTenant: "globex", tokenTenant: "acme");
        allowed.Should().BeFalse();
    }

    [Fact]
    public async Task Denies_WhenTokenCarriesNoTenantClaim()
    {
        var allowed = await EvaluateAsync(routeTenant: "acme", tokenTenant: null);
        allowed.Should().BeFalse();
    }
}
