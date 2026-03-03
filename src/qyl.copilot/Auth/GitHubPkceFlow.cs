// =============================================================================
// qyl.copilot - GitHub OAuth PKCE Flow
// Implements RFC 7636 PKCE for GitHub OAuth Apps (added Nov 2023).
// Usage:
//   var (url, state, verifier) = GitHubPkceFlow.Start(options);
//   // redirect user to url, store state+verifier in session
//   var result = await GitHubPkceFlow.ExchangeAsync(code, state, storedState, verifier, options, ct);
//   provider.SetOAuthToken(result.Token!, result.Username, result.ExpiresAt);
// =============================================================================

using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace qyl.copilot.Auth;

/// <summary>
///     GitHub OAuth PKCE flow (RFC 7636).
///     Completes the <c>AuthMethod.OAuth</c> path in <see cref="CopilotAuthProvider" />.
/// </summary>
public static partial class GitHubPkceFlow
{
    private const string AuthorizeEndpoint = "https://github.com/login/oauth/authorize";
    private const string TokenEndpoint = "https://github.com/login/oauth/access_token";
    private const string UserEndpoint = "https://api.github.com/user";
    private const string DefaultScopes = "read:user copilot";

    // ── Step 1 & 2: Generate verifier + challenge, build authorization URL ────

    /// <summary>
    ///     Starts the PKCE flow. Returns the authorization URL to redirect the user to,
    ///     plus the opaque <paramref name="state" /> and <paramref name="codeVerifier" />
    ///     that must be stored for the callback.
    /// </summary>
    /// <param name="options">Auth options with ClientId and CallbackUrl.</param>
    /// <param name="scopes">OAuth scopes (defaults to "read:user copilot").</param>
    /// <returns>Authorization URL, CSRF state token, and PKCE code verifier.</returns>
    public static (string AuthorizationUrl, string State, string CodeVerifier) Start(
        CopilotAuthOptions options,
        string? scopes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.OAuthClientId,
            nameof(options.OAuthClientId));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.OAuthCallbackUrl,
            nameof(options.OAuthCallbackUrl));

        var codeVerifier = GenerateCodeVerifier();
        var codeChallenge = DeriveCodeChallenge(codeVerifier);
        var state = GenerateState();

        var query = new Dictionary<string, string>
        {
            ["client_id"] = options.OAuthClientId,
            ["redirect_uri"] = options.OAuthCallbackUrl,
            ["scope"] = scopes ?? DefaultScopes,
            ["state"] = state,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256"
        };

        var url = AuthorizeEndpoint + "?" + string.Join("&",
            query.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        return (url, state, codeVerifier);
    }

    // ── Step 7 & 8 & 9: Exchange authorization code + verifier for token ─────

    /// <summary>
    ///     Exchanges the authorization <paramref name="code" /> for an access token,
    ///     completing the PKCE flow.
    /// </summary>
    /// <param name="code">Authorization code from GitHub callback.</param>
    /// <param name="state">State from GitHub callback (for CSRF validation).</param>
    /// <param name="expectedState">State stored when <see cref="Start" /> was called.</param>
    /// <param name="codeVerifier">Verifier stored when <see cref="Start" /> was called.</param>
    /// <param name="options">Auth options with ClientId, ClientSecret, and CallbackUrl.</param>
    /// <param name="httpClient">Optional HttpClient (creates one if null).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="AuthResult" /> with token and GitHub username, or failure.</returns>
    public static async ValueTask<AuthResult> ExchangeAsync(
        string code,
        string state,
        string expectedState,
        string codeVerifier,
        CopilotAuthOptions options,
        HttpClient? httpClient = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedState);
        ArgumentException.ThrowIfNullOrWhiteSpace(codeVerifier);

        // CSRF check
        if (!string.Equals(state, expectedState, StringComparison.Ordinal))
        {
            return AuthResult.Failed("State mismatch — possible CSRF attack.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(options.OAuthClientId, nameof(options.OAuthClientId));

        var ownedClient = httpClient is null;
        httpClient ??= CreateDefaultHttpClient();

        try
        {
            // ── Exchange code + verifier for access token ──
            var tokenResponse = await FetchAccessTokenAsync(
                httpClient, code, codeVerifier, options, ct).ConfigureAwait(false);

            if (tokenResponse?.AccessToken is not { } accessToken)
            {
                return AuthResult.Failed(
                    tokenResponse?.ErrorDescription ?? tokenResponse?.Error ?? "Token exchange failed.");
            }

            // ── Fetch GitHub username ──
            var username = await FetchUsernameAsync(httpClient, accessToken, ct).ConfigureAwait(false);

            return AuthResult.Succeeded(
                accessToken,
                AuthMethod.OAuth,
                username,
                // GitHub tokens don't expire by default; expiry may be present for fine-grained PATs
                expiresAt: null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            return AuthResult.Failed($"Token exchange network error: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return AuthResult.Failed($"Token exchange response parse error: {ex.Message}");
        }
        finally
        {
            if (ownedClient) httpClient.Dispose();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    ///     Generates a cryptographically secure code verifier (43 URL-safe chars).
    ///     RFC 7636 §4.1: 43–128 unreserved ASCII chars.
    /// </summary>
    private static string GenerateCodeVerifier()
    {
        Span<byte> buf = stackalloc byte[32]; // 32 bytes → 43 Base64url chars
        RandomNumberGenerator.Fill(buf);
        return Base64UrlEncode(buf);
    }

    /// <summary>
    ///     Derives the PKCE code challenge from the verifier.
    ///     RFC 7636 §4.2: code_challenge = BASE64URL(SHA256(ASCII(code_verifier)))
    /// </summary>
    private static string DeriveCodeChallenge(string codeVerifier)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier), hash);
        return Base64UrlEncode(hash);
    }

    /// <summary>Generates a random CSRF state token.</summary>
    private static string GenerateState()
    {
        Span<byte> buf = stackalloc byte[16];
        RandomNumberGenerator.Fill(buf);
        return Convert.ToHexString(buf).ToLowerInvariant();
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> input) =>
        Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static async Task<GitHubTokenResponse?> FetchAccessTokenAsync(
        HttpClient client,
        string code,
        string codeVerifier,
        CopilotAuthOptions options,
        CancellationToken ct)
    {
        var body = new Dictionary<string, string>
        {
            ["client_id"] = options.OAuthClientId!,
            ["code"] = code,
            ["redirect_uri"] = options.OAuthCallbackUrl ?? string.Empty,
            ["code_verifier"] = codeVerifier
        };

        // Include client_secret when present (confidential client; PKCE is defence-in-depth)
        if (!string.IsNullOrWhiteSpace(options.OAuthClientSecret))
        {
            body["client_secret"] = options.OAuthClientSecret;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(body),
            Headers = { { "Accept", "application/json" } }
        };

        using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync(GitHubTokenResponseContext.Default.GitHubTokenResponse, ct)
            .ConfigureAwait(false);
    }

    private static async Task<string?> FetchUsernameAsync(
        HttpClient client,
        string accessToken,
        CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, UserEndpoint);
            request.Headers.Authorization = new("Bearer", accessToken);

            using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;

            var user = await response.Content
                .ReadFromJsonAsync(GitHubUserContext.Default.GitHubUser, ct)
                .ConfigureAwait(false);
            return user?.Login;
        }
        catch (HttpRequestException)
        {
            return null; // username fetch is best-effort
        }
        catch (JsonException)
        {
            return null; // username fetch is best-effort
        }
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("qyl-copilot/1.0");
        return client;
    }

    // ── AOT-safe JSON types ───────────────────────────────────────────────────

    private sealed record GitHubTokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("token_type")] string? TokenType,
        [property: JsonPropertyName("scope")] string? Scope,
        [property: JsonPropertyName("error")] string? Error,
        [property: JsonPropertyName("error_description")] string? ErrorDescription
    );

    private sealed record GitHubUser(
        [property: JsonPropertyName("login")] string? Login
    );

    [JsonSerializable(typeof(GitHubTokenResponse))]
    private sealed partial class GitHubTokenResponseContext : JsonSerializerContext;

    [JsonSerializable(typeof(GitHubUser))]
    private sealed partial class GitHubUserContext : JsonSerializerContext;
}
