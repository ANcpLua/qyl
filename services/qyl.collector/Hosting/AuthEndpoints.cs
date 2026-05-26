using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Qyl.Collector.Auth;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Hosting;

/// <summary>
/// `/auth/*` endpoints driving the OIDC authorization-code + PKCE flow that
/// mints opaque MCP tokens. Registered from Program.cs adjacent to
/// <see cref="CollectorEndpointExtensions.MapQylCollectorEndpoints"/>.
/// </summary>
/// <remarks>
/// All routes under `/auth/` are excluded from <c>TokenAuthMiddleware</c>
/// (see <see cref="CollectorAuthExtensions"/>) — the whole point of these
/// endpoints is to ESTABLISH auth, so they cannot themselves require an
/// existing token.
/// </remarks>
public static partial class AuthEndpoints
{
    private const string Scopes = "openid profile email offline_access";

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "Keycloak discovery unreachable on /auth/authorize")]
    private static partial void LogDiscoveryUnreachable(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "Keycloak discovery failed on /auth/authorize")]
    private static partial void LogDiscoveryFailed(ILogger logger, Exception ex);

    public static IEndpointRouteBuilder MapQylAuth(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/auth");
        group.MapGet("/authorize", AuthorizeAsync);
        // /auth/callback — E1.c
        // /auth/refresh, /auth/revoke — E1.d
        return routes;
    }

    /// <summary>
    /// Per qyl-PRD Stage E1.b. Validates the requested client redirect URI
    /// against the allowlist, mints PKCE state + nonce, stores the row in
    /// <c>mcp_pkce_state</c> BEFORE returning the redirect (no race), and
    /// 302s the user-agent at Keycloak's <c>authorization_endpoint</c> with
    /// all required PKCE/OIDC query params.
    /// </summary>
    /// <param name="tenant">Tenant identifier the resulting token will scope to.</param>
    /// <param name="redirect_uri">Where the client wants the opaque token delivered.
    /// Must be exact-match against <see cref="KeycloakOptions.AllowedRedirects"/>
    /// unless the allowlist is empty (dev only).</param>
    internal static async Task<IResult> AuthorizeAsync(
        [FromQuery] string? tenant,
        [FromQuery(Name = "redirect_uri")] string? redirect_uri,
        HttpContext context,
        IKeycloakClient keycloak,
        IPkceStateStore pkceStore,
        IOptions<KeycloakOptions> optionsAccessor,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Qyl.Collector.Auth.Authorize");
        if (string.IsNullOrWhiteSpace(tenant))
            return Results.BadRequest(new ErrorResponse("invalid_request", "tenant query parameter is required"));

        if (string.IsNullOrWhiteSpace(redirect_uri))
            return Results.BadRequest(new ErrorResponse("invalid_request", "redirect_uri query parameter is required"));

        var options = optionsAccessor.Value;
        if (!options.IsEnabled)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Keycloak not configured",
                detail: $"Set {KeycloakOptions.AuthorityEnvVar} + {KeycloakOptions.ClientIdEnvVar} to enable /auth/* endpoints.");
        }

        if (!IsAllowedRedirect(redirect_uri, options.AllowedRedirects))
        {
            return Results.BadRequest(new ErrorResponse(
                "redirect_uri_not_allowed",
                $"redirect_uri is not in {KeycloakOptions.AllowedRedirectsEnvVar} allowlist."));
        }

        KeycloakDiscoveryDocument discovery;
        try
        {
            discovery = await keycloak.GetDiscoveryDocumentAsync(ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            LogDiscoveryUnreachable(logger, ex);
            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Keycloak discovery unreachable",
                detail: "The upstream identity provider is not responding. Retry shortly.");
        }
        catch (InvalidOperationException ex)
        {
            LogDiscoveryFailed(logger, ex);
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Keycloak discovery failed",
                detail: "Authentication is temporarily unavailable.");
        }

        // 32 bytes of entropy on `state` matches the opaque-token shape.
        // 64 bytes for code_verifier yields ~86 chars base64url (within RFC 7636's 43-128 range).
        // 16 bytes for nonce = 128 bits, sufficient for id_token replay protection.
        var state = TokenGenerator.Generate(byteLength: 32);
        var codeVerifier = TokenGenerator.Generate(byteLength: 64);
        var nonce = TokenGenerator.Generate(byteLength: 16);
        var codeChallenge = ComputeS256Challenge(codeVerifier);

        // PRD invariant: store BEFORE returning the redirect. If this throws, the user-agent
        // never reaches Keycloak, so no orphaned authorization request can resolve later.
        await pkceStore.StoreAsync(
            state,
            codeVerifier,
            tenant,
            redirect_uri,
            nonce,
            options.PkceStateTtl,
            ct).ConfigureAwait(false);

        var collectorCallback = BuildCollectorCallbackUri(context);

        var redirectUrl = QueryHelpers.AddQueryString(
            discovery.AuthorizationEndpoint,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["response_type"] = "code",
                ["client_id"] = options.ClientId!,
                ["redirect_uri"] = collectorCallback,
                ["scope"] = Scopes,
                ["state"] = state,
                ["code_challenge"] = codeChallenge,
                ["code_challenge_method"] = "S256",
                ["nonce"] = nonce,
            });

        return Results.Redirect(redirectUrl);
    }

    /// <summary>
    /// Exact-match allowlist check. Empty allowlist = pass-through (dev / unset).
    /// Per PRD, mismatch → 400 with `redirect_uri_not_allowed`.
    /// </summary>
    private static bool IsAllowedRedirect(string uri, string[] allowlist) =>
        allowlist.Length == 0 || Array.IndexOf(allowlist, uri) >= 0;

    /// <summary>
    /// RFC 7636 §4.2: <c>code_challenge = BASE64URL(SHA256(code_verifier))</c>.
    /// </summary>
    private static string ComputeS256Challenge(string codeVerifier)
    {
        Span<byte> hash = stackalloc byte[32];
        if (!SHA256.TryHashData(Encoding.ASCII.GetBytes(codeVerifier), hash, out _))
            throw new InvalidOperationException("SHA256.TryHashData failed");
        return Convert.ToBase64String(hash)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>
    /// Reconstruct the collector's public callback URL from the current request.
    /// This is the URI Keycloak redirects to AFTER the user logs in; distinct
    /// from the client's <c>redirect_uri</c> (where we eventually deliver the
    /// opaque token via /auth/callback's own 302).
    /// </summary>
    private static string BuildCollectorCallbackUri(HttpContext context)
    {
        var request = context.Request;
        return $"{request.Scheme}://{request.Host}{request.PathBase}/auth/callback";
    }
}
