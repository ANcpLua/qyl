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

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(header) || !BearerHeader.TryExtract(header, out var opaque) || opaque is null)
            return AuthenticateResult.NoResult();

        var record = await tokenStore.GetByOpaqueTokenAsync(opaque, Context.RequestAborted).ConfigureAwait(false);
        if (record is null)
            return AuthenticateResult.Fail("invalid token");

        // Identity only. The route {tenant} == token TenantId check is an AUTHORIZATION concern
        // (McpTenantMatchRequirement), so a valid token at the wrong tenant yields 403 (authenticated
        // but forbidden), not 401. The route is addressing; the token tenant claim below is authority.
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
