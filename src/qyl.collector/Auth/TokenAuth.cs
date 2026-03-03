using System.Security.Claims;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace qyl.collector.Auth;

// ── Keycloak JWKS validation ──────────────────────────────────────────────────

/// <summary>
///     Validates a JWT via Keycloak's JWKS endpoint and returns the claims on success,
///     or <see langword="null"/> on failure.
/// </summary>
public interface IKeycloakJwksValidator
{
    ValueTask<IReadOnlyDictionary<string, string>?> ValidateAsync(string token, CancellationToken ct = default);
}

/// <summary>
///     No-op sentinel used when <c>QYL_KEYCLOAK_AUTHORITY</c> is not set.
///     Always returns <see langword="null"/>, preserving existing auth behaviour unchanged.
/// </summary>
internal sealed class NullKeycloakJwksValidator : IKeycloakJwksValidator
{
    public static readonly NullKeycloakJwksValidator Instance = new();

    public ValueTask<IReadOnlyDictionary<string, string>?> ValidateAsync(string token, CancellationToken ct)
        => ValueTask.FromResult<IReadOnlyDictionary<string, string>?>(null);
}

/// <summary>
///     Validates JWTs against Keycloak's JWKS endpoint.
///     Signing keys are cached for <see cref="KeysCacheDuration"/> and refreshed automatically
///     on <see cref="SecurityTokenSignatureKeyNotFoundException"/>.
/// </summary>
internal sealed partial class KeycloakJwksValidator : IKeycloakJwksValidator, IDisposable
{
    private static readonly JsonWebTokenHandler TokenHandler = new();
    private static readonly TimeSpan KeysCacheDuration = TimeSpan.FromHours(1);

    private readonly string _authority;
    private readonly string? _audience;
    private readonly HttpClient _httpClient;
    private readonly ILogger<KeycloakJwksValidator> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private IReadOnlyList<SecurityKey> _cachedKeys = [];
    private DateTimeOffset _keysExpiry = DateTimeOffset.MinValue;

    public KeycloakJwksValidator(
        string authority,
        string? audience,
        HttpClient httpClient,
        ILogger<KeycloakJwksValidator> logger)
    {
        _authority = authority.TrimEnd('/');
        _audience = audience;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async ValueTask<IReadOnlyDictionary<string, string>?> ValidateAsync(
        string token, CancellationToken ct = default)
    {
        IReadOnlyList<SecurityKey> keys = await GetKeysAsync(ct).ConfigureAwait(false);
        TokenValidationResult result = await ValidateWithKeysAsync(token, keys).ConfigureAwait(false);

        // On stale signing key: force-refresh keys and retry once
        if (!result.IsValid && result.Exception is SecurityTokenSignatureKeyNotFoundException)
        {
            LogKeysExpired();
            await _refreshLock.WaitAsync(ct).ConfigureAwait(false);
            try { _keysExpiry = DateTimeOffset.MinValue; }
            finally { _refreshLock.Release(); }

            keys = await GetKeysAsync(ct).ConfigureAwait(false);
            result = await ValidateWithKeysAsync(token, keys).ConfigureAwait(false);
        }

        if (!result.IsValid)
        {
            LogValidationFailed(result.Exception?.Message ?? "unknown");
            return null;
        }

        return ExtractClaims(result);
    }

    private async ValueTask<IReadOnlyList<SecurityKey>> GetKeysAsync(CancellationToken ct)
    {
        DateTimeOffset now = TimeProvider.System.GetUtcNow();

        // Fast path: return cached keys without acquiring the semaphore
        if (_cachedKeys.Count > 0 && now < _keysExpiry)
            return _cachedKeys;

        await _refreshLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Re-check after acquiring lock (another caller may have refreshed)
            if (_cachedKeys.Count > 0 && TimeProvider.System.GetUtcNow() < _keysExpiry)
                return _cachedKeys;

            return await RefreshKeysAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async ValueTask<IReadOnlyList<SecurityKey>> RefreshKeysAsync(CancellationToken ct)
    {
        string jwksUri = $"{_authority}/protocol/openid-connect/certs";
        try
        {
            string json = await _httpClient.GetStringAsync(jwksUri, ct).ConfigureAwait(false);
            JsonWebKeySet jwks = new(json);
            IReadOnlyList<SecurityKey> keys = jwks.GetSigningKeys();
            _cachedKeys = keys;
            _keysExpiry = TimeProvider.System.GetUtcNow().Add(KeysCacheDuration);
            LogKeysRefreshed(keys.Count);
            return keys;
        }
        catch (HttpRequestException ex)
        {
            // Network failure (Keycloak unreachable): return stale keys rather than breaking auth
            LogKeysRefreshFailed(jwksUri, ex.Message);
            return _cachedKeys;
        }
        catch (System.Text.Json.JsonException ex)
        {
            // Malformed JWKS JSON: return stale keys
            LogKeysRefreshFailed(jwksUri, ex.Message);
            return _cachedKeys;
        }
    }

    private Task<TokenValidationResult> ValidateWithKeysAsync(string token, IReadOnlyList<SecurityKey> keys)
    {
        TokenValidationParameters parameters = new()
        {
            ValidateIssuer = true,
            ValidIssuer = _authority,
            ValidateAudience = _audience is not null,
            ValidAudience = _audience,
            ValidateLifetime = true,
            IssuerSigningKeys = keys,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
        return TokenHandler.ValidateTokenAsync(token, parameters);
    }

    private static IReadOnlyDictionary<string, string> ExtractClaims(TokenValidationResult result)
    {
        Dictionary<string, string> claims = new(StringComparer.OrdinalIgnoreCase);
        foreach (Claim claim in result.ClaimsIdentity.Claims)
            claims.TryAdd(claim.Type, claim.Value);
        return claims.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    public void Dispose() => _refreshLock.Dispose();

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Keycloak signing keys refreshed — {KeyCount} keys cached for 1 hour")]
    private partial void LogKeysRefreshed(int keyCount);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Keycloak signing key not found in cache — forcing JWKS refresh")]
    private partial void LogKeysExpired();

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "JWT validation failed: {Reason}")]
    private partial void LogValidationFailed(string reason);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Failed to refresh Keycloak JWKS from {Uri}: {Error}")]
    private partial void LogKeysRefreshFailed(string uri, string error);
}

public static class TokenGenerator
{
    public static string Generate(int byteLength = 24)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(byteLength);

        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteLength))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}

public sealed class TokenAuthOptions
{
    /// <summary>
    ///     HTTP header name for MCP API key authentication (Aspire pattern).
    /// </summary>
    public const string McpApiKeyHeader = "x-mcp-api-key";

    /// <summary>
    ///     Gets or sets the auth token. Auto-generates a secure token if not explicitly set.
    /// </summary>
    public string Token
    {
        get => field ??= TokenGenerator.Generate();
        set;
    }

    /// <summary>
    ///     Gets or sets the cookie name for auth token storage.
    /// </summary>
    public string CookieName { get; set; } = "qyl_options.Token";

    /// <summary>
    ///     Gets or sets cookie expiration in days. Must be positive.
    /// </summary>
    public int CookieExpirationDays
    {
        get;
        set => field = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "Cookie expiration must be positive");
    } = 3;

    /// <summary>
    ///     Gets or sets the query parameter name for token in URL.
    /// </summary>
    public string QueryParameterName { get; set; } = "t";

    /// <summary>
    ///     Gets or sets paths excluded from token authentication.
    ///     Only health probes and OTLP ingestion paths are excluded.
    ///     All /api/* endpoints REQUIRE authentication.
    /// </summary>
    public string[] ExcludedPaths { get; set; } =
        ["/health", "/ready", "/alive", "/v1/traces", "/v1/logs", "/v1/metrics"];

    /// <summary>
    ///     <see cref="HttpContext.Items"/> key under which validated Keycloak claims are stored.
    ///     Value is an <see cref="IReadOnlyDictionary{TKey,TValue}"/> of claim type → value.
    /// </summary>
    public const string KeycloakClaimsKey = "qyl.keycloak.claims";
}

public sealed class TokenAuthMiddleware(
    RequestDelegate next,
    TokenAuthOptions options,
    IKeycloakJwksValidator keycloakValidator)
{
    public async Task InvokeAsync(HttpContext context)
    {
        string path = context.Request.Path.Value ?? "/";

        // Allow dashboard root and static files without auth
        if (path == "/" || IsStaticFile(path))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        if (options.ExcludedPaths.Any(path.StartsWithIgnoreCase))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        string? queryToken = context.Request.Query[options.QueryParameterName].FirstOrDefault();
        if (!string.IsNullOrEmpty(queryToken) && ValidateToken(queryToken))
        {
            SetAuthCookie(context);

            string cleanUrl = RemoveQueryParameter(context.Request, options.QueryParameterName);
            context.Response.Redirect(cleanUrl);
            return;
        }

        string? cookieToken = context.Request.Cookies[options.CookieName];
        if (!string.IsNullOrEmpty(cookieToken) && ValidateToken(cookieToken))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        string? authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader))
        {
            string bearerToken = authHeader.StartsWithIgnoreCase("Bearer ")
                ? authHeader[7..]
                : authHeader;

            if (ValidateToken(bearerToken))
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            // Symmetric check failed; try Keycloak JWKS for JWT Bearer tokens (contain '.')
            if (keycloakValidator is not NullKeycloakJwksValidator && bearerToken.Contains('.'))
            {
                IReadOnlyDictionary<string, string>? claims = await keycloakValidator
                    .ValidateAsync(bearerToken, context.RequestAborted)
                    .ConfigureAwait(false);

                if (claims is not null)
                {
                    context.Items[TokenAuthOptions.KeycloakClaimsKey] = claims;
                    await next(context).ConfigureAwait(false);
                    return;
                }
            }
        }

        // Check x-mcp-api-key header (Aspire pattern for MCP server authentication)
        var mcpApiKey = context.Request.Headers[TokenAuthOptions.McpApiKeyHeader].FirstOrDefault();
        if (!string.IsNullOrEmpty(mcpApiKey) && ValidateToken(mcpApiKey))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        if (path.StartsWithIgnoreCase("/api/"))
        {
            context.Response.StatusCode = 401;
            context.Response.Headers.WWWAuthenticate = "Bearer";
            await context.Response.WriteAsJsonAsync(
                    new ErrorResponse("Unauthorized", "Valid token required"),
                    QylSerializerContext.Default.ErrorResponse)
                .ConfigureAwait(false);
            return;
        }

        await next(context).ConfigureAwait(false);
    }

    private bool ValidateToken(string token) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(token),
            Encoding.UTF8.GetBytes(options.Token));

    private void SetAuthCookie(HttpContext context) =>
        context.Response.Cookies.Append(options.CookieName, options.Token,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Expires = TimeProvider.System.GetUtcNow().AddDays(options.CookieExpirationDays),
                Path = "/"
            });

    private static string RemoveQueryParameter(HttpRequest request, string paramName)
    {
        var query = QueryHelpers.ParseQuery(request.QueryString.Value);
        query.Remove(paramName);

        var newQuery = QueryHelpers.AddQueryString(
            request.PathBase + request.Path, query);

        return newQuery;
    }

    public string GetToken() => options.Token;

    private static bool IsStaticFile(string path)
    {
        // Common static file extensions for dashboard assets
        ReadOnlySpan<string> staticExtensions =
        [
            ".js", ".css", ".html", ".htm", ".json",
            ".png", ".jpg", ".jpeg", ".gif", ".svg", ".ico", ".webp",
            ".woff", ".woff2", ".ttf", ".eot",
            ".map", ".txt"
        ];

        foreach (var ext in staticExtensions)
        {
            if (path.EndsWithIgnoreCase(ext))
                return true;
        }

        return false;
    }
}

public static class TokenAuthExtensions
{
    public static IApplicationBuilder UseTokenAuth(this IApplicationBuilder app,
        Action<TokenAuthOptions>? configure = null)
    {
        var options = new TokenAuthOptions();
        configure?.Invoke(options);

        return app.UseMiddleware<TokenAuthMiddleware>(options);
    }
}
