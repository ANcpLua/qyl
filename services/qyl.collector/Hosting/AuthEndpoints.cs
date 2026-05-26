using System.Globalization;
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

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning,
        Message = "Keycloak returned authorization error {Error} on /auth/callback")]
    private static partial void LogAuthorizationError(ILogger logger, string error);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning,
        Message = "PKCE state row missing/expired on /auth/callback")]
    private static partial void LogPkceMissing(ILogger logger);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning,
        Message = "Keycloak rejected authorization-code exchange on /auth/callback")]
    private static partial void LogTokenExchangeFailed(ILogger logger);

    [LoggerMessage(EventId = 6, Level = LogLevel.Warning,
        Message = "id_token signature/audience/issuer/lifetime validation failed on /auth/callback")]
    private static partial void LogIdTokenInvalid(ILogger logger);

    [LoggerMessage(EventId = 7, Level = LogLevel.Warning,
        Message = "id_token nonce mismatch on /auth/callback — possible replay")]
    private static partial void LogNonceMismatch(ILogger logger);

    [LoggerMessage(EventId = 8, Level = LogLevel.Warning,
        Message = "id_token missing required claim {Claim} on /auth/callback")]
    private static partial void LogIdTokenMissingClaim(ILogger logger, string claim);

    public static IEndpointRouteBuilder MapQylAuth(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/auth");
        group.MapGet("/authorize", AuthorizeAsync);
        group.MapGet("/callback", CallbackAsync);
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

    /// <summary>
    /// Per qyl-PRD Stage E1.c. Consumes the PKCE state row, exchanges the
    /// authorization code for tokens at Keycloak's <c>token_endpoint</c>,
    /// validates the <c>id_token</c> JWT (signature, audience, issuer,
    /// lifetime, nonce binding), encrypts the upstream refresh_token,
    /// mints an opaque MCP token via <see cref="IMcpTokenStore"/>, and
    /// 302s the user-agent back at the client redirect with the opaque
    /// token in the URL fragment (never the query — fragments aren't
    /// logged by proxies).
    /// </summary>
    /// <param name="state">Random opaque value minted by /auth/authorize, identifies the PKCE row to consume.</param>
    /// <param name="code">Authorization code returned by Keycloak.</param>
    /// <param name="error">Set by Keycloak when the user denies consent or the request is otherwise rejected.</param>
    /// <param name="errorDescription">Human-readable detail accompanying <paramref name="error"/>.</param>
    internal static async Task<IResult> CallbackAsync(
        [FromQuery] string? state,
        [FromQuery] string? code,
        [FromQuery] string? error,
        [FromQuery(Name = "error_description")] string? errorDescription,
        HttpContext context,
        IKeycloakClient keycloak,
        IKeycloakJwksValidator jwksValidator,
        IPkceStateStore pkceStore,
        IMcpTokenStore tokenStore,
        ITokenEncryption encryption,
        TimeProvider timeProvider,
        IOptions<KeycloakOptions> optionsAccessor,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Qyl.Collector.Auth.Callback");

        // Surface user-denied / Keycloak-rejected authorizations directly.
        if (!string.IsNullOrEmpty(error))
        {
            LogAuthorizationError(logger, error);
            return Results.BadRequest(new ErrorResponse(
                error,
                errorDescription ?? "Keycloak rejected the authorization request."));
        }

        if (string.IsNullOrWhiteSpace(state))
            return Results.BadRequest(new ErrorResponse("invalid_request", "state query parameter is required"));

        if (string.IsNullOrWhiteSpace(code))
            return Results.BadRequest(new ErrorResponse("invalid_request", "code query parameter is required"));

        var options = optionsAccessor.Value;
        if (!options.IsEnabled)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Keycloak not configured",
                detail: $"Set {KeycloakOptions.AuthorityEnvVar} + {KeycloakOptions.ClientIdEnvVar} to enable /auth/* endpoints.");
        }

        // Single-use, TTL-gated. Second call with the same state returns null.
        var pkce = await pkceStore.ConsumeAsync(state, ct).ConfigureAwait(false);
        if (pkce is null)
        {
            LogPkceMissing(logger);
            return Results.BadRequest(new ErrorResponse(
                "invalid_state",
                "state is unknown, expired, or already consumed."));
        }

        // Same redirect_uri value MUST be sent that /authorize sent — Keycloak binds them.
        var collectorCallback = BuildCollectorCallbackUri(context);
        var tokens = await keycloak.ExchangeAuthorizationCodeAsync(
            code, pkce.CodeVerifier, collectorCallback, ct).ConfigureAwait(false);
        if (tokens is null)
        {
            LogTokenExchangeFailed(logger);
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Token exchange rejected",
                detail: "The authorization code or PKCE verifier was not accepted.");
        }

        var claims = await jwksValidator.ValidateAsync(tokens.IdToken, ct).ConfigureAwait(false);
        if (claims is null)
        {
            LogIdTokenInvalid(logger);
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "id_token invalid",
                detail: "Token signature, audience, issuer, or lifetime validation failed.");
        }

        // Nonce binding: validator doesn't check nonce; we must. Fixed-time compare to
        // prevent timing oracles on partial-match-length leaks.
        if (!claims.TryGetValue("nonce", out var nonceClaim) ||
            !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(nonceClaim),
                Encoding.UTF8.GetBytes(pkce.Nonce)))
        {
            LogNonceMismatch(logger);
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "id_token nonce mismatch",
                detail: "id_token did not bind to this authorization request.");
        }

        if (!claims.TryGetValue("sub", out var subject) || string.IsNullOrWhiteSpace(subject))
        {
            LogIdTokenMissingClaim(logger, "sub");
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "id_token incomplete",
                detail: "Token did not carry a stable user identifier.");
        }

        var encryptedRefresh = encryption.Encrypt(Encoding.UTF8.GetBytes(tokens.RefreshToken));
        var refreshExpiresAt = timeProvider.GetUtcNow().AddSeconds(tokens.RefreshExpiresIn);

        var issued = await tokenStore.CreateAsync(new McpTokenCreate(
            UserId: subject,
            TenantId: pkce.TenantId,
            Scopes: tokens.Scope ?? string.Empty,
            EncryptedRefresh: encryptedRefresh,
            RefreshExpiresAt: refreshExpiresAt), ct).ConfigureAwait(false);

        return Results.Redirect(BuildClientCallbackUri(pkce.ClientRedirectUri, issued.OpaqueToken, refreshExpiresAt));
    }

    /// <summary>
    /// Build the fragment-bearing redirect back to the client. Token goes in
    /// the URL fragment, never the query string — fragments are not sent in
    /// the <c>Referer</c> header and not logged by HTTP proxies.
    /// </summary>
    private static string BuildClientCallbackUri(string clientRedirectUri, string opaqueToken, DateTimeOffset expiresAt)
    {
        var separator = clientRedirectUri.Contains('#') ? '&' : '#';
        var expiresIso = expiresAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        return string.Concat(
            clientRedirectUri,
            separator.ToString(),
            "token=", Uri.EscapeDataString(opaqueToken),
            "&expires_at=", Uri.EscapeDataString(expiresIso));
    }
}
