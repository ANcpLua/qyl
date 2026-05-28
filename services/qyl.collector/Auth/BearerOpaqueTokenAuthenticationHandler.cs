using System.Security.Claims;
using System.Text.Encodings.Web;
using ANcpLua.Roslyn.Utilities.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Auth;

internal sealed class BearerOpaqueTokenAuthenticationHandler(
    IOptionsMonitor<BearerOpaqueTokenAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IMcpTokenStore tokenStore)
    : AuthenticationHandler<BearerOpaqueTokenAuthenticationOptions>(options, logger, encoder)
{
    internal const string TenantClaimType = "qyl.tenant_id";
    private const string TenantRouteKey = "tenant";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(header) || !BearerHeader.TryExtract(header, out var opaque) || opaque is null)
            return AuthenticateResult.NoResult();

        var record = await tokenStore.GetByOpaqueTokenAsync(opaque, Context.RequestAborted).ConfigureAwait(false);
        if (record is null)
            return AuthenticateResult.Fail("invalid token");

        // Confused-deputy guard: a token is valid only at its own tenant's route. A tenant
        // mismatch reports the same failure as a bad token so a caller cannot probe which
        // tenants a token is valid for.
        var routeTenant = Context.Request.RouteValues.TryGetValue(TenantRouteKey, out var raw)
            ? raw as string
            : null;
        if (string.IsNullOrEmpty(routeTenant)
            || !string.Equals(routeTenant, record.TenantId, StringComparison.Ordinal))
            return AuthenticateResult.Fail("invalid token");

        if (Options.TouchLastUsed)
            await tokenStore.TouchLastUsedAsync(record.TokenHash, Context.RequestAborted).ConfigureAwait(false);

        var identity = new ClaimsIdentity(Scheme.Name);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, record.UserId));
        identity.AddClaim(new Claim(TenantClaimType, record.TenantId));
        foreach (var scope in record.Scopes.Split(
                     ' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            identity.AddClaim(new Claim("scope", scope));

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }
}
