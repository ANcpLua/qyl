using System.Collections.Frozen;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace qyl.mcp.Auth;

/// <summary>
///     Fetches and caches a Keycloak JWT via OAuth2 client-credentials flow.
///     Returns <see langword="null"/> when Keycloak is not configured — callers fall back to the API-key path.
/// </summary>
public sealed partial class KeycloakTokenProvider : IDisposable
{
    public const string HttpClientName = "KeycloakTokenProvider";

    private readonly ILogger<KeycloakTokenProvider> _logger;
    private readonly McpAuthOptions _options;
    private readonly HttpClient _httpClient;
    private readonly TimeProvider _time;
    private readonly Lock _lock = new();

    private string? _cachedToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;
    private FrozenSet<string> _cachedRoles = FrozenSet<string>.Empty;

    public KeycloakTokenProvider(
        IOptions<McpAuthOptions> options,
        HttpClient httpClient,
        TimeProvider time,
        ILogger<KeycloakTokenProvider> logger)
    {
        _options = options.Value;
        _httpClient = httpClient;
        _time = time;
        _logger = logger;
    }

    /// <summary>
    ///     Returns a valid Bearer token string, or <see langword="null"/> when Keycloak is not configured.
    ///     Refreshes automatically when the cached token is within 60 seconds of expiry.
    /// </summary>
    public async ValueTask<string?> GetTokenAsync(CancellationToken ct = default)
    {
        if (!_options.IsKeycloakEnabled)
            return null;

        using (_lock.EnterScope())
        {
            if (_cachedToken is not null &&
                _time.GetUtcNow() < _tokenExpiry.AddSeconds(-60))
            {
                return _cachedToken;
            }
        }

        return await FetchTokenAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    ///     Returns the set of Keycloak realm roles from the most recently fetched token.
    ///     Empty when no token has been fetched yet or Keycloak is not configured.
    /// </summary>
    public FrozenSet<string> GetCachedRoles()
    {
        using (_lock.EnterScope())
            return _cachedRoles;
    }

    // Network failures (HttpRequestException — Keycloak unreachable) intentionally propagate
    // so the resilience handler on the named HttpClient can retry and circuit-break.
    private async ValueTask<string?> FetchTokenAsync(CancellationToken ct)
    {
        string authority = _options.KeycloakAuthority!.TrimEnd('/');
        string tokenEndpoint = $"{authority}/protocol/openid-connect/token";

        using FormUrlEncodedContent form = new(
        [
            new("grant_type",    "client_credentials"),
            new("client_id",     _options.KeycloakClientId!),
            new("client_secret", _options.KeycloakClientSecret!),
        ]);

        HttpResponseMessage response = await _httpClient
            .PostAsync(tokenEndpoint, form, ct)
            .ConfigureAwait(false);

        // Non-2xx from Keycloak (bad credentials, realm not found, etc.) — log and return null
        // so callers can fall back to the API-key path rather than crashing the MCP server.
        if (!response.IsSuccessStatusCode)
        {
            LogTokenHttpError((int)response.StatusCode, tokenEndpoint);
            return null;
        }

        TokenResponse? tokenResponse;
        try
        {
            tokenResponse = await response.Content
                .ReadFromJsonAsync(KeycloakJsonContext.Default.TokenResponse, ct)
                .ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            LogTokenJsonError(ex);
            return null;
        }

        if (tokenResponse is null || string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
        {
            LogTokenResponseEmpty();
            return null;
        }

        FrozenSet<string> roles = ExtractRoles(tokenResponse.AccessToken);
        DateTimeOffset expiry = _time.GetUtcNow().AddSeconds(tokenResponse.ExpiresIn);

        using (_lock.EnterScope())
        {
            _cachedToken = tokenResponse.AccessToken;
            _tokenExpiry = expiry;
            _cachedRoles = roles;
        }

        LogTokenFetched(tokenResponse.ExpiresIn, roles.Count);
        return tokenResponse.AccessToken;
    }

    /// <summary>
    ///     Decodes the JWT payload without signature validation (the token was just fetched directly
    ///     from Keycloak's token endpoint, so its provenance is trusted) and extracts
    ///     <c>realm_access.roles</c>.
    /// </summary>
    private static FrozenSet<string> ExtractRoles(string jwt)
    {
        ReadOnlySpan<char> span = jwt;
        int firstDot = span.IndexOf('.');
        if (firstDot < 0) return FrozenSet<string>.Empty;

        ReadOnlySpan<char> remainder = span[(firstDot + 1)..];
        int secondDot = remainder.IndexOf('.');
        if (secondDot < 0) return FrozenSet<string>.Empty;

        string payload = new string(remainder[..secondDot])
            .Replace('-', '+')
            .Replace('_', '/');

        payload = (payload.Length % 4) switch
        {
            2 => payload + "==",
            3 => payload + "=",
            _ => payload
        };

        try
        {
            byte[] bytes = Convert.FromBase64String(payload);
            using JsonDocument doc = JsonDocument.Parse(bytes);

            if (!doc.RootElement.TryGetProperty("realm_access", out JsonElement realmAccess) ||
                !realmAccess.TryGetProperty("roles", out JsonElement rolesArray))
            {
                return FrozenSet<string>.Empty;
            }

            HashSet<string> roles = [];
            foreach (JsonElement role in rolesArray.EnumerateArray())
            {
                string? roleStr = role.GetString();
                if (!string.IsNullOrWhiteSpace(roleStr))
                    roles.Add(roleStr);
            }

            return roles.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return FrozenSet<string>.Empty;
        }
    }

    public void Dispose() => _httpClient.Dispose();

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Keycloak token fetched — expires in {ExpiresIn}s, {RoleCount} realm roles")]
    private partial void LogTokenFetched(int expiresIn, int roleCount);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Keycloak token endpoint {Endpoint} returned HTTP {StatusCode} — check client credentials and realm config")]
    private partial void LogTokenHttpError(int statusCode, string endpoint);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Keycloak token response was empty or missing access_token")]
    private partial void LogTokenResponseEmpty();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Keycloak token response contained invalid JSON")]
    private partial void LogTokenJsonError(JsonException ex);
}

// ── Token response DTO ────────────────────────────────────────────────────────

internal sealed record TokenResponse(
    [property: JsonPropertyName("access_token")]  string  AccessToken,
    [property: JsonPropertyName("expires_in")]    int     ExpiresIn,
    [property: JsonPropertyName("token_type")]    string  TokenType);

[JsonSerializable(typeof(TokenResponse))]
internal sealed partial class KeycloakJsonContext : JsonSerializerContext;
