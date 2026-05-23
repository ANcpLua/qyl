using System.Collections.Frozen;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace qyl.mcp.Auth;

public sealed partial class KeycloakTokenProvider : IDisposable
{
    public const string HttpClientName = "KeycloakTokenProvider";

    private readonly HttpClient _httpClient;
    private readonly Lock _lock = new();

    private readonly ILogger<KeycloakTokenProvider> _logger;
    private readonly McpAuthOptions _options;
    private readonly TimeProvider _time;
    private FrozenSet<string> _cachedRoles = FrozenSet<string>.Empty;

    private string? _cachedToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;

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

    public void Dispose() => _httpClient.Dispose();

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

    public FrozenSet<string> GetCachedRoles()
    {
        using (_lock.EnterScope())
            return _cachedRoles;
    }

    private async ValueTask<string?> FetchTokenAsync(CancellationToken ct)
    {
        var authorityValue = _options.KeycloakAuthority;
        var clientId = _options.KeycloakClientId;
        var clientSecret = _options.KeycloakClientSecret;

        if (string.IsNullOrWhiteSpace(authorityValue) ||
            string.IsNullOrWhiteSpace(clientId) ||
            string.IsNullOrWhiteSpace(clientSecret))
        {
            return null;
        }

        var authority = authorityValue.TrimEnd('/');
        var tokenEndpoint = $"{authority}/protocol/openid-connect/token";

        using FormUrlEncodedContent form = new(
        [
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("client_secret", clientSecret)
        ]);

        var response = await _httpClient
            .PostAsync(tokenEndpoint, form, ct)
            .ConfigureAwait(false);

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

        var roles = ExtractRoles(tokenResponse.AccessToken);
        var expiry = _time.GetUtcNow().AddSeconds(tokenResponse.ExpiresIn);

        using (_lock.EnterScope())
        {
            _cachedToken = tokenResponse.AccessToken;
            _tokenExpiry = expiry;
            _cachedRoles = roles;
        }

        LogTokenFetched(tokenResponse.ExpiresIn, roles.Count);
        return tokenResponse.AccessToken;
    }

    private static FrozenSet<string> ExtractRoles(string jwt)
    {
        ReadOnlySpan<char> span = jwt;
        var firstDot = span.IndexOf('.');
        if (firstDot < 0) return FrozenSet<string>.Empty;

        var remainder = span[(firstDot + 1)..];
        var secondDot = remainder.IndexOf('.');
        if (secondDot < 0) return FrozenSet<string>.Empty;

        var payload = new string(remainder[..secondDot])
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
            var bytes = Convert.FromBase64String(payload);
            using var doc = JsonDocument.Parse(bytes);

            if (!doc.RootElement.TryGetProperty("realm_access", out var realmAccess) ||
                !realmAccess.TryGetProperty("roles", out var rolesArray))
            {
                return FrozenSet<string>.Empty;
            }

            HashSet<string> roles = [];
            foreach (var role in rolesArray.EnumerateArray())
            {
                var roleStr = role.GetString();
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

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Keycloak token fetched — expires in {ExpiresIn}s, {RoleCount} realm roles")]
    private partial void LogTokenFetched(int expiresIn, int roleCount);

    [LoggerMessage(Level = LogLevel.Warning,
        Message =
            "Keycloak token endpoint {Endpoint} returned HTTP {StatusCode} — check client credentials and realm config")]
    private partial void LogTokenHttpError(int statusCode, string endpoint);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Keycloak token response was empty or missing access_token")]
    private partial void LogTokenResponseEmpty();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Keycloak token response contained invalid JSON")]
    private partial void LogTokenJsonError(JsonException ex);
}


internal sealed record TokenResponse(
    [property: JsonPropertyName("access_token")]
    string AccessToken,
    [property: JsonPropertyName("expires_in")]
    int ExpiresIn,
    [property: JsonPropertyName("token_type")]
    string TokenType);

[JsonSerializable(typeof(TokenResponse))]
internal sealed partial class KeycloakJsonContext : JsonSerializerContext;
