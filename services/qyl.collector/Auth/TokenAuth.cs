using ANcpLua.Roslyn.Utilities.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using ANcpLua.Roslyn.Utilities;
namespace Qyl.Collector.Auth;


public interface IKeycloakJwksValidator
{
    ValueTask<IReadOnlyDictionary<string, string>?> ValidateAsync(string token, CancellationToken ct = default);
}

internal sealed class NullKeycloakJwksValidator : IKeycloakJwksValidator
{
    public static readonly NullKeycloakJwksValidator Instance = new();

    public ValueTask<IReadOnlyDictionary<string, string>?> ValidateAsync(string token, CancellationToken ct)
        => ValueTask.FromResult<IReadOnlyDictionary<string, string>?>(null);
}

internal sealed partial class KeycloakJwksValidator : IKeycloakJwksValidator, IDisposable
{
    private static readonly JsonWebTokenHandler s_tokenHandler = new();
    private static readonly TimeSpan s_keysCacheDuration = TimeSpan.FromHours(1);
    private readonly string? _audience;

    private readonly string _authority;
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

    public void Dispose() => _refreshLock.Dispose();

    public async ValueTask<IReadOnlyDictionary<string, string>?> ValidateAsync(
        string token, CancellationToken ct = default)
    {
        var keys = await GetKeysAsync(ct).ConfigureAwait(false);
        var result = await ValidateWithKeysAsync(token, keys).ConfigureAwait(false);

        if (result is { IsValid: false, Exception: SecurityTokenSignatureKeyNotFoundException })
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
        var now = TimeProvider.System.GetUtcNow();

        if (_cachedKeys.Count > 0 && now < _keysExpiry)
            return _cachedKeys;

        await _refreshLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
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
        var jwksUri = $"{_authority}/protocol/openid-connect/certs";
        try
        {
            var json = await _httpClient.GetStringAsync(jwksUri, ct).ConfigureAwait(false);
            JsonWebKeySet jwks = new(json);
            var keys = (IReadOnlyList<SecurityKey>)jwks.GetSigningKeys();
            _cachedKeys = keys;
            _keysExpiry = TimeProvider.System.GetUtcNow().Add(s_keysCacheDuration);
            LogKeysRefreshed(keys.Count);
            return keys;
        }
        catch (HttpRequestException ex)
        {
            LogKeysRefreshFailed(jwksUri, ex.Message);
            return _cachedKeys;
        }
        catch (JsonException ex)
        {
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
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            IssuerSigningKeys = keys,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        return s_tokenHandler.ValidateTokenAsync(token, parameters);
    }

    private static FrozenDictionary<string, string> ExtractClaims(TokenValidationResult result)
    {
        Dictionary<string, string> claims = new(StringComparer.OrdinalIgnoreCase);
        foreach (var claim in result.ClaimsIdentity.Claims)
            claims.TryAdd(claim.Type, claim.Value);
        return claims.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

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
        Guard.Positive(byteLength);

        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteLength))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}

public sealed class TokenAuthOptions
{
    public const string McpApiKeyHeader = "x-mcp-api-key";

    public const string KeycloakClaimsKey = QylAttr.Auth.KeycloakClaims;

    public string Token
    {
        get => field ??= TokenGenerator.Generate();
        set;
    }

    public string CookieName { get; set; } = "qyl_options.Token";

    public int CookieExpirationDays
    {
        get;
        set => field = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "Cookie expiration must be positive");
    } = 3;

    public string QueryParameterName { get; set; } = "t";

    public string[] ExcludedPaths { get; set; } =
        ["/health", "/alive", "/health/ui", "/v1/traces", "/v1/logs", "/v1/metrics"];
}

public sealed class TokenAuthMiddleware(
    RequestDelegate next,
    TokenAuthOptions options,
    IKeycloakJwksValidator keycloakValidator)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";

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

        var queryToken = context.Request.Query[options.QueryParameterName].FirstOrDefault();
        if (!string.IsNullOrEmpty(queryToken) && ValidateToken(queryToken))
        {
            SetAuthCookie(context);

            var cleanUrl = RemoveQueryParameter(context.Request, options.QueryParameterName);
            context.Response.Redirect(cleanUrl);
            return;
        }

        var cookieToken = context.Request.Cookies[options.CookieName];
        if (!string.IsNullOrEmpty(cookieToken) && ValidateToken(cookieToken))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader))
        {
            var bearerToken = BearerHeader.TryExtract(authHeader, out var t)
                ? t!
                : authHeader;

            if (ValidateToken(bearerToken))
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            if (keycloakValidator is not NullKeycloakJwksValidator && bearerToken.Contains('.'))
            {
                var claims = await keycloakValidator
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
