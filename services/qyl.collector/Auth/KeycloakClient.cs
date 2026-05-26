using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Qyl.Collector.Auth;

/// <summary>
/// Subset of the OIDC discovery document (RFC 8414 + OIDC core 1.0) that the
/// collector's /auth/* endpoints consume. Field names are the snake_case shape
/// Keycloak emits at <c>{authority}/.well-known/openid-configuration</c>.
/// </summary>
public sealed record KeycloakDiscoveryDocument(
    [property: JsonPropertyName("authorization_endpoint")] string AuthorizationEndpoint,
    [property: JsonPropertyName("token_endpoint")] string TokenEndpoint,
    [property: JsonPropertyName("jwks_uri")] string JwksUri,
    [property: JsonPropertyName("end_session_endpoint")] string EndSessionEndpoint,
    [property: JsonPropertyName("issuer")] string Issuer,
    [property: JsonPropertyName("revocation_endpoint")] string? RevocationEndpoint = null);

/// <summary>Successful token-endpoint response (auth_code or refresh_token grant).</summary>
public sealed record KeycloakTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("id_token")] string IdToken,
    [property: JsonPropertyName("refresh_token")] string RefreshToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("refresh_expires_in")] int RefreshExpiresIn,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("scope")] string? Scope = null);

[JsonSerializable(typeof(KeycloakDiscoveryDocument))]
[JsonSerializable(typeof(KeycloakTokenResponse))]
internal sealed partial class KeycloakClientJsonContext : JsonSerializerContext;

/// <summary>
/// OIDC client for the user-facing authorization-code + PKCE flow. Single
/// instance per process; thread-safe via a <c>System.Threading.Lock</c>
/// guarding the discovery cache.
/// </summary>
/// <remarks>
/// Distinct from <c>qyl.mcp/Auth/KeycloakTokenProvider</c> which is the
/// service-account client-credentials path for MCP → Keycloak introspection.
/// This client drives end-user browser logins → opaque-token mint via /auth/*.
/// </remarks>
public interface IKeycloakClient
{
    /// <summary>Fetches (and caches for <see cref="KeycloakOptions.DiscoveryCacheDuration"/>) the discovery doc.</summary>
    /// <exception cref="InvalidOperationException">Keycloak is unconfigured or discovery endpoint returned null.</exception>
    Task<KeycloakDiscoveryDocument> GetDiscoveryDocumentAsync(CancellationToken ct);

    /// <summary>Authorization-code + PKCE grant. Returns null on 4xx (rejected by Keycloak).</summary>
    Task<KeycloakTokenResponse?> ExchangeAuthorizationCodeAsync(
        string code, string codeVerifier, string redirectUri, CancellationToken ct);

    /// <summary>Refresh-token grant. Null = refresh expired/revoked upstream.</summary>
    Task<KeycloakTokenResponse?> ExchangeRefreshTokenAsync(string refreshToken, CancellationToken ct);

    /// <summary>RFC 7009 token revocation. Best-effort — exceptions logged, swallowed (local revoke is source of truth).</summary>
    Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct);

    /// <summary>Force the cache to refetch on the next call. Invoked when JWKS signature validation fails (the key may have rotated).</summary>
    void InvalidateDiscoveryDocument();
}

internal sealed partial class KeycloakClient : IKeycloakClient
{
    public const string HttpClientName = "Keycloak";

    private readonly Uri _discoveryUri;
    private readonly HttpClient _httpClient;
    private readonly KeycloakOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<KeycloakClient> _logger;
    private readonly Lock _cacheLock = new();

    private KeycloakDiscoveryDocument? _cached;
    private DateTimeOffset _cachedAt;

    public KeycloakClient(
        HttpClient httpClient,
        IOptions<KeycloakOptions> options,
        TimeProvider timeProvider,
        ILogger<KeycloakClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.Authority))
        {
            throw new InvalidOperationException(
                $"{KeycloakOptions.AuthorityEnvVar} is required to use IKeycloakClient. " +
                "Register NullKeycloakClient.Instance in DI when Keycloak is unconfigured.");
        }

        _discoveryUri = new Uri($"{_options.Authority.TrimEnd('/')}/.well-known/openid-configuration");
    }

    public async Task<KeycloakDiscoveryDocument> GetDiscoveryDocumentAsync(CancellationToken ct)
    {
        var now = _timeProvider.GetUtcNow();

        lock (_cacheLock)
        {
            if (_cached is { } cached && now - _cachedAt < _options.DiscoveryCacheDuration)
                return cached;
        }

        var fresh = await _httpClient
            .GetFromJsonAsync(_discoveryUri, KeycloakClientJsonContext.Default.KeycloakDiscoveryDocument, ct)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Keycloak discovery document at {_discoveryUri} returned null.");

        lock (_cacheLock)
        {
            _cached = fresh;
            _cachedAt = now;
        }

        LogDiscoveryRefreshed(fresh.Issuer);
        return fresh;
    }

    public void InvalidateDiscoveryDocument()
    {
        lock (_cacheLock)
        {
            _cached = null;
        }
        LogDiscoveryInvalidated();
    }

    public Task<KeycloakTokenResponse?> ExchangeAuthorizationCodeAsync(
        string code, string codeVerifier, string redirectUri, CancellationToken ct)
    {
        var form = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["code_verifier"] = codeVerifier,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = _options.ClientId ?? string.Empty,
        };
        if (!string.IsNullOrEmpty(_options.ClientSecret))
            form["client_secret"] = _options.ClientSecret;

        return PostTokenEndpointAsync(form, ct);
    }

    public Task<KeycloakTokenResponse?> ExchangeRefreshTokenAsync(string refreshToken, CancellationToken ct)
    {
        var form = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = _options.ClientId ?? string.Empty,
        };
        if (!string.IsNullOrEmpty(_options.ClientSecret))
            form["client_secret"] = _options.ClientSecret;

        return PostTokenEndpointAsync(form, ct);
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct)
    {
        KeycloakDiscoveryDocument discovery;
        try
        {
            discovery = await GetDiscoveryDocumentAsync(ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            LogRevocationException(ex.Message);
            return;
        }

        if (string.IsNullOrEmpty(discovery.RevocationEndpoint))
        {
            LogRevocationUnsupported();
            return;
        }

        var form = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["token"] = refreshToken,
            ["token_type_hint"] = "refresh_token",
            ["client_id"] = _options.ClientId ?? string.Empty,
        };
        if (!string.IsNullOrEmpty(_options.ClientSecret))
            form["client_secret"] = _options.ClientSecret;

        try
        {
            using var content = new FormUrlEncodedContent(form);
            using var response = await _httpClient.PostAsync(discovery.RevocationEndpoint, content, ct)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                LogRevocationFailed((int)response.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            LogRevocationException(ex.Message);
        }
    }

    private async Task<KeycloakTokenResponse?> PostTokenEndpointAsync(
        Dictionary<string, string> form, CancellationToken ct)
    {
        KeycloakDiscoveryDocument discovery;
        try
        {
            discovery = await GetDiscoveryDocumentAsync(ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            LogTokenEndpointException(ex.Message);
            return null;
        }

        try
        {
            using var content = new FormUrlEncodedContent(form);
            using var response = await _httpClient.PostAsync(discovery.TokenEndpoint, content, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                LogTokenEndpointRejected((int)response.StatusCode);
                return null;
            }

            return await response.Content
                .ReadFromJsonAsync(KeycloakClientJsonContext.Default.KeycloakTokenResponse, ct)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            LogTokenEndpointException(ex.Message);
            return null;
        }
        catch (JsonException ex)
        {
            LogTokenEndpointMalformed(ex.Message);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Keycloak discovery refreshed — issuer={Issuer}")]
    private partial void LogDiscoveryRefreshed(string issuer);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Keycloak discovery cache invalidated — will refetch on next call")]
    private partial void LogDiscoveryInvalidated();

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Keycloak token endpoint returned {StatusCode}")]
    private partial void LogTokenEndpointRejected(int statusCode);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Keycloak token endpoint request failed: {Error}")]
    private partial void LogTokenEndpointException(string error);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Keycloak token endpoint returned malformed JSON: {Error}")]
    private partial void LogTokenEndpointMalformed(string error);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Keycloak discovery doc has no revocation_endpoint; skipping revoke call")]
    private partial void LogRevocationUnsupported();

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Keycloak revocation returned {StatusCode} — local revoke still applies")]
    private partial void LogRevocationFailed(int statusCode);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Keycloak revocation request failed ({Error}) — local revoke still applies")]
    private partial void LogRevocationException(string error);
}

/// <summary>Null implementation returned from DI when Keycloak is unconfigured. Makes /auth/* endpoints safe to register unconditionally.</summary>
internal sealed class NullKeycloakClient : IKeycloakClient
{
    public static readonly NullKeycloakClient Instance = new();

    private static readonly InvalidOperationException s_unconfigured = new(
        $"Keycloak is not configured. Set {KeycloakOptions.AuthorityEnvVar} + {KeycloakOptions.ClientIdEnvVar} to enable /auth/* endpoints.");

    public Task<KeycloakDiscoveryDocument> GetDiscoveryDocumentAsync(CancellationToken ct)
        => Task.FromException<KeycloakDiscoveryDocument>(s_unconfigured);

    public Task<KeycloakTokenResponse?> ExchangeAuthorizationCodeAsync(
        string code, string codeVerifier, string redirectUri, CancellationToken ct)
        => Task.FromResult<KeycloakTokenResponse?>(null);

    public Task<KeycloakTokenResponse?> ExchangeRefreshTokenAsync(string refreshToken, CancellationToken ct)
        => Task.FromResult<KeycloakTokenResponse?>(null);

    public Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct) => Task.CompletedTask;

    public void InvalidateDiscoveryDocument() { }
}
