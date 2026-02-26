namespace qyl.collector.Identity;

/// <summary>
///     GitHub identity integration with runtime-updatable tokens (ADR-002).
///     Supports: env var, PAT paste, GitHub Device Flow.
///     Singleton service — uses Lock for thread-safe token updates.
/// </summary>
public sealed partial class GitHubService(
    IHttpClientFactory httpClientFactory,
    DuckDbStore store,
    IConfiguration configuration,
    ILogger<GitHubService> logger)
{
    private readonly string? _envToken = configuration["QYL_GITHUB_TOKEN"];
    private readonly string? _clientId = configuration["QYL_GITHUB_CLIENT_ID"];
    private readonly Lock _tokenLock = new();
    private string? _runtimeToken;
    private string _authMethod = "none";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(GetEffectiveToken());

    /// <summary>
    ///     Returns the current GitHub token (runtime or env var). Used by qyl.copilot token bridge (ADR-002).
    /// </summary>
    public string? GetToken() => GetEffectiveToken();

    public string AuthMethod
    {
        get
        {
            lock (_tokenLock)
                return _authMethod;
        }
    }

    public bool IsDeviceFlowAvailable => !string.IsNullOrWhiteSpace(_clientId);

    /// <summary>
    ///     Loads persisted token from DuckDB on startup, falls back to env var.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var record = await store.GetGitHubTokenAsync(ct).ConfigureAwait(false);
        if (record is not null)
        {
            lock (_tokenLock)
            {
                _runtimeToken = record.Token;
                _authMethod = record.AuthMethod;
            }

            LogTokenLoaded(record.AuthMethod, record.GitHubLogin ?? "unknown");
        }
        else if (!string.IsNullOrWhiteSpace(_envToken))
        {
            lock (_tokenLock)
                _authMethod = "env";

            LogTokenLoaded("env", "from environment variable");
        }
    }

    /// <summary>
    ///     Validates a token against GitHub /user, persists to DuckDB, updates in-memory.
    /// </summary>
    public async Task<GitHubUser?> SetTokenAsync(string token, string authMethod = "pat", CancellationToken ct = default)
    {
        var user = await ValidateTokenAsync(token, ct).ConfigureAwait(false);
        if (user is null)
            return null;

        await store.UpsertGitHubTokenAsync(token, "repo", user.Login, authMethod, ct).ConfigureAwait(false);

        lock (_tokenLock)
        {
            _runtimeToken = token;
            _authMethod = authMethod;
        }

        LogTokenSaved(authMethod, user.Login);
        return user;
    }

    /// <summary>
    ///     Clears persisted token, reverts to env var if present.
    /// </summary>
    public async Task ClearTokenAsync(CancellationToken ct = default)
    {
        await store.DeleteGitHubTokenAsync(ct).ConfigureAwait(false);

        lock (_tokenLock)
        {
            _runtimeToken = null;
            _authMethod = !string.IsNullOrWhiteSpace(_envToken) ? "env" : "none";
        }

        LogTokenCleared();
    }

    public async Task<GitHubRepo[]> GetRepositoriesAsync(CancellationToken ct = default)
    {
        if (!IsConfigured)
            return [];

        using var client = CreateClient();
        var response = await client.GetAsync("user/repos?sort=updated&per_page=30", ct)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            LogGitHubApiFailed("user/repos", (int)response.StatusCode);
            return [];
        }

        var repos = await response.Content
            .ReadFromJsonAsync(GitHubJsonContext.Default.GitHubRepoArray, ct)
            .ConfigureAwait(false);

        return repos ?? [];
    }

    public async Task<GitHubUser?> GetUserAsync(CancellationToken ct = default)
    {
        if (!IsConfigured)
            return null;

        using var client = CreateClient();
        var response = await client.GetAsync("user", ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            LogGitHubApiFailed("user", (int)response.StatusCode);
            return null;
        }

        return await response.Content
            .ReadFromJsonAsync(GitHubJsonContext.Default.GitHubUser, ct)
            .ConfigureAwait(false);
    }

    // ==========================================================================
    // Device Flow
    // ==========================================================================

    public async Task<DeviceCodeResponse?> StartDeviceFlowAsync(CancellationToken ct = default)
    {
        if (!IsDeviceFlowAvailable)
            return null;

        using var client = httpClientFactory.CreateClient();
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _clientId!,
            ["scope"] = "repo"
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/device/code")
        {
            Content = content
        };
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        var response = await client.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            LogGitHubApiFailed("device/code", (int)response.StatusCode);
            return null;
        }

        return await response.Content
            .ReadFromJsonAsync(GitHubJsonContext.Default.DeviceCodeResponse, ct)
            .ConfigureAwait(false);
    }

    public async Task<DevicePollResponse> PollDeviceFlowAsync(string deviceCode, CancellationToken ct = default)
    {
        if (!IsDeviceFlowAvailable)
            return new DevicePollResponse("error", null, "Device flow not configured");

        using var client = httpClientFactory.CreateClient();
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _clientId!,
            ["device_code"] = deviceCode,
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code"
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token")
        {
            Content = content
        };
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        var response = await client.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return new DevicePollResponse("error", null, "GitHub API error");

        var tokenResponse = await response.Content
            .ReadFromJsonAsync(GitHubJsonContext.Default.DeviceTokenResponse, ct)
            .ConfigureAwait(false);

        if (tokenResponse is null)
            return new DevicePollResponse("error", null, "Invalid response");

        if (!string.IsNullOrEmpty(tokenResponse.Error))
        {
            return tokenResponse.Error switch
            {
                "authorization_pending" => new DevicePollResponse("pending", null, null),
                "slow_down" => new DevicePollResponse("pending", null, null),
                "expired_token" => new DevicePollResponse("expired", null, "Device code expired"),
                "access_denied" => new DevicePollResponse("denied", null, "Access denied by user"),
                _ => new DevicePollResponse("error", null, tokenResponse.ErrorDescription)
            };
        }

        if (string.IsNullOrEmpty(tokenResponse.AccessToken))
            return new DevicePollResponse("error", null, "No access token in response");

        // Token received — validate and persist
        var user = await SetTokenAsync(tokenResponse.AccessToken, "device_flow", ct).ConfigureAwait(false);
        return user is not null
            ? new DevicePollResponse("complete", user, null)
            : new DevicePollResponse("error", null, "Token validation failed");
    }

    // ==========================================================================
    // Private
    // ==========================================================================

    private string? GetEffectiveToken()
    {
        lock (_tokenLock)
            return _runtimeToken ?? _envToken;
    }

    private async Task<GitHubUser?> ValidateTokenAsync(string token, CancellationToken ct)
    {
        using var client = CreateClientWithToken(token);
        var response = await client.GetAsync("user", ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            LogGitHubApiFailed("user (validation)", (int)response.StatusCode);
            return null;
        }

        return await response.Content
            .ReadFromJsonAsync(GitHubJsonContext.Default.GitHubUser, ct)
            .ConfigureAwait(false);
    }

    private HttpClient CreateClient()
    {
        var effectiveToken = GetEffectiveToken();
        var client = httpClientFactory.CreateClient("GitHub");
        if (!string.IsNullOrWhiteSpace(effectiveToken))
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", effectiveToken);
        return client;
    }

    private HttpClient CreateClientWithToken(string token)
    {
        var client = httpClientFactory.CreateClient("GitHub");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ==========================================================================
    // LoggerMessage -- structured, zero-allocation logging
    // ==========================================================================

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "GitHub API call failed: {Endpoint} returned {StatusCode}")]
    private partial void LogGitHubApiFailed(string endpoint, int statusCode);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "GitHub token loaded: method={AuthMethod}, user={User}")]
    private partial void LogTokenLoaded(string authMethod, string user);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "GitHub token saved: method={AuthMethod}, user={User}")]
    private partial void LogTokenSaved(string authMethod, string user);

    [LoggerMessage(Level = LogLevel.Information, Message = "GitHub token cleared")]
    private partial void LogTokenCleared();
}

// =============================================================================
// GitHub DTOs
// =============================================================================

public sealed record GitHubRepo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("full_name")] string FullName,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("html_url")] string HtmlUrl,
    [property: JsonPropertyName("private")] bool Private,
    [property: JsonPropertyName("default_branch")] string DefaultBranch);

public sealed record GitHubUser(
    [property: JsonPropertyName("login")] string Login,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("avatar_url")] string? AvatarUrl);

public sealed record GitHubTokenRequest(
    [property: JsonPropertyName("token")] string Token);

public sealed record GitHubStatusResponse(
    [property: JsonPropertyName("configured")] bool Configured,
    [property: JsonPropertyName("user")] GitHubUser? User,
    [property: JsonPropertyName("authMethod")] string AuthMethod = "none");

public sealed record DeviceCodeResponse(
    [property: JsonPropertyName("device_code")] string DeviceCode,
    [property: JsonPropertyName("user_code")] string UserCode,
    [property: JsonPropertyName("verification_uri")] string VerificationUri,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("interval")] int Interval);

public sealed record DeviceTokenResponse(
    [property: JsonPropertyName("access_token")] string? AccessToken,
    [property: JsonPropertyName("token_type")] string? TokenType,
    [property: JsonPropertyName("scope")] string? Scope,
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("error_description")] string? ErrorDescription);

public sealed record DevicePollResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("user")] GitHubUser? User,
    [property: JsonPropertyName("error")] string? Error);

public sealed record DeviceAvailableResponse(
    [property: JsonPropertyName("available")] bool Available);

// =============================================================================
// Source-generated JSON context for GitHub types
// =============================================================================

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(GitHubRepo))]
[JsonSerializable(typeof(GitHubRepo[]))]
[JsonSerializable(typeof(GitHubUser))]
[JsonSerializable(typeof(GitHubTokenRequest))]
[JsonSerializable(typeof(GitHubStatusResponse))]
[JsonSerializable(typeof(DeviceCodeResponse))]
[JsonSerializable(typeof(DeviceTokenResponse))]
[JsonSerializable(typeof(DevicePollResponse))]
[JsonSerializable(typeof(DeviceAvailableResponse))]
internal partial class GitHubJsonContext : JsonSerializerContext;
