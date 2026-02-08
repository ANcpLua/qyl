// =============================================================================
// qyl.copilot - Copilot Authentication Provider
// Cascading auth detection: Environment -> gh CLI -> PAT -> OAuth
// OTel 1.39 compliant instrumentation
// =============================================================================

using qyl.protocol.Copilot;

namespace qyl.copilot.Auth;

/// <summary>
///     Authentication options for GitHub Copilot.
/// </summary>
public sealed record CopilotAuthOptions
{
    /// <summary>Auto-detect token from environment (GH_TOKEN, GITHUB_TOKEN, gh CLI).</summary>
    public bool AutoDetect { get; init; } = true;

    /// <summary>Explicit Personal Access Token (fallback).</summary>
    public string? PersonalAccessToken { get; init; }

    /// <summary>OAuth callback URL for dashboard-initiated flow.</summary>
    public string? OAuthCallbackUrl { get; init; }

    /// <summary>OAuth client ID (for dashboard flow).</summary>
    public string? OAuthClientId { get; init; }

    /// <summary>OAuth client secret (for dashboard flow, should be from secrets).</summary>
    public string? OAuthClientSecret { get; init; }
}

/// <summary>
///     Authentication method used to obtain the token.
/// </summary>
public enum AuthMethod
{
    /// <summary>No authentication available.</summary>
    None,

    /// <summary>From GH_TOKEN or GITHUB_TOKEN environment variable.</summary>
    EnvironmentVariable,

    /// <summary>From GitHub CLI (gh auth token).</summary>
    GitHubCli,

    /// <summary>From explicit PAT in configuration.</summary>
    PersonalAccessToken,

    /// <summary>From OAuth flow via dashboard.</summary>
    OAuth
}

/// <summary>
///     Result of authentication attempt.
/// </summary>
public sealed record AuthResult
{
    /// <summary>Whether authentication succeeded.</summary>
    public required bool Success { get; init; }

    /// <summary>The obtained token (null if failed).</summary>
    public string? Token { get; init; }

    /// <summary>Method used to obtain the token.</summary>
    public AuthMethod Method { get; init; }

    /// <summary>Error message if authentication failed.</summary>
    public string? Error { get; init; }

    /// <summary>GitHub username if authenticated.</summary>
    public string? Username { get; init; }

    /// <summary>Token expiration if known.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Creates a failed result.</summary>
    public static AuthResult Failed(string error) => new() { Success = false, Error = error, Method = AuthMethod.None };

    /// <summary>Creates a successful result.</summary>
    public static AuthResult Succeeded(string token, AuthMethod method, string? username = null,
        DateTimeOffset? expiresAt = null) => new()
    {
        Success = true,
        Token = token,
        Method = method,
        Username = username,
        ExpiresAt = expiresAt
    };
}

/// <summary>
///     Provides cascading authentication for GitHub Copilot.
///     Detection order: Environment -> gh CLI -> PAT -> OAuth
/// </summary>
public sealed class CopilotAuthProvider
{
    private readonly Lock _lock = new();
    private readonly CopilotAuthOptions _options;
    private readonly TimeProvider _timeProvider;

    private AuthResult? _cachedResult;
    private DateTimeOffset _cacheExpiry;

    /// <summary>
    ///     Creates a new auth provider with the specified options.
    /// </summary>
    public CopilotAuthProvider(CopilotAuthOptions? options = null, TimeProvider? timeProvider = null)
    {
        _options = options ?? new CopilotAuthOptions();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    ///     Attempts to obtain a valid authentication token.
    ///     Uses cascading detection: Environment -> gh CLI -> PAT -> OAuth
    /// </summary>
    public async ValueTask<AuthResult> GetTokenAsync(CancellationToken ct = default)
    {
        // Check cache first
        using (_lock.EnterScope())
        {
            if (_cachedResult is { Success: true } && _timeProvider.GetUtcNow() < _cacheExpiry)
            {
                return _cachedResult;
            }
        }

        AuthResult result;

        if (_options.AutoDetect)
        {
            // 1. Try environment variables (GH_TOKEN, GITHUB_TOKEN)
            result = TryEnvironmentVariable();
            if (result.Success)
            {
                CacheResult(result);
                return result;
            }

            // 2. Try GitHub CLI (gh auth token)
            result = await TryGitHubCliAsync(ct).ConfigureAwait(false);
            if (result.Success)
            {
                CacheResult(result);
                return result;
            }
        }

        // 3. Try explicit PAT
        if (!string.IsNullOrEmpty(_options.PersonalAccessToken))
        {
            result = AuthResult.Succeeded(_options.PersonalAccessToken, AuthMethod.PersonalAccessToken);
            CacheResult(result);
            return result;
        }

        // 4. OAuth would be triggered externally and the token set via SetOAuthToken
        return AuthResult.Failed(
            "No authentication method available. Set GH_TOKEN, GITHUB_TOKEN, configure gh CLI, or provide a PAT.");
    }

    /// <summary>
    ///     Sets the OAuth token after successful OAuth flow.
    /// </summary>
    public void SetOAuthToken(string token, string? username = null, DateTimeOffset? expiresAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var result = AuthResult.Succeeded(token, AuthMethod.OAuth, username, expiresAt);
        CacheResult(result);
    }

    /// <summary>
    ///     Clears the cached authentication result.
    /// </summary>
    public void ClearCache()
    {
        using (_lock.EnterScope())
        {
            _cachedResult = null;
        }
    }

    /// <summary>
    ///     Gets the current authentication status.
    /// </summary>
    public async ValueTask<CopilotAuthStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var result = await GetTokenAsync(ct).ConfigureAwait(false);

        return new CopilotAuthStatus
        {
            IsAuthenticated = result.Success,
            AuthMethod = result.Method.ToString(),
            Username = result.Username,
            ExpiresAt = result.ExpiresAt,
            Error = result.Error,
            Capabilities = result.Success ? ["chat", "workflow", "tools"] : null
        };
    }

    private void CacheResult(AuthResult result)
    {
        using (_lock.EnterScope())
        {
            _cachedResult = result;
            // Cache for 5 minutes or until expiry, whichever is sooner
            var defaultExpiry = _timeProvider.GetUtcNow().AddMinutes(5);
            _cacheExpiry = result.ExpiresAt.HasValue && result.ExpiresAt.Value < defaultExpiry
                ? result.ExpiresAt.Value
                : defaultExpiry;
        }
    }

    private static AuthResult TryEnvironmentVariable()
    {
        // Try GH_TOKEN first (preferred by gh CLI)
        var token = Environment.GetEnvironmentVariable("GH_TOKEN");
        if (!string.IsNullOrWhiteSpace(token))
        {
            return AuthResult.Succeeded(token, AuthMethod.EnvironmentVariable);
        }

        // Fall back to GITHUB_TOKEN (common in CI/CD)
        token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (!string.IsNullOrWhiteSpace(token))
        {
            return AuthResult.Succeeded(token, AuthMethod.EnvironmentVariable);
        }

        return AuthResult.Failed("No GH_TOKEN or GITHUB_TOKEN environment variable found.");
    }

    private static async ValueTask<AuthResult> TryGitHubCliAsync(CancellationToken ct)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "gh",
                    Arguments = "auth token",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            // Use a timeout to prevent hanging on gh CLI issues
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

            var tokenTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Timeout occurred, kill the process
                try { process.Kill(); } catch { /* ignore */ }
                return AuthResult.Failed("gh auth token timed out after 10 seconds");
            }

            if (process.ExitCode is 0)
            {
                var token = (await tokenTask.ConfigureAwait(false)).Trim();
                if (!string.IsNullOrWhiteSpace(token))
                {
                    // Try to get username
                    var username = await TryGetGitHubUsernameAsync(ct).ConfigureAwait(false);
                    return AuthResult.Succeeded(token, AuthMethod.GitHubCli, username);
                }
            }

            var error = await errorTask.ConfigureAwait(false);
            return AuthResult.Failed($"gh auth token failed: {error}");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // Propagate user cancellation
        }
        catch (Exception ex)
        {
            return AuthResult.Failed($"GitHub CLI not available: {ex.Message}");
        }
    }

    private static async ValueTask<string?> TryGetGitHubUsernameAsync(CancellationToken ct)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "gh",
                Arguments = "api user --jq .login",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
            await process.WaitForExitAsync(ct).ConfigureAwait(false);

            return process.ExitCode is 0 ? output.Trim() : null;
        }
        catch
        {
            return null;
        }
    }
}
