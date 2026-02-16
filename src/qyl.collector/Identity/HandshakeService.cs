namespace qyl.collector.Identity;

/// <summary>
///     Handshake session state machine with PKCE challenge/verifier validation.
///     State transitions: pending -> verified -> expired.
///     Singleton service — writes via DuckDbStore channel, reads via pooled connections.
/// </summary>
public sealed partial class HandshakeService(DuckDbStore store, ILogger<HandshakeService> logger)
{
    private static readonly TimeSpan HandshakeExpiry = TimeSpan.FromMinutes(10);

    /// <summary>
    ///     Initiates a handshake session with a PKCE code_challenge.
    ///     Generates a workspace ID and stores the session in pending state.
    /// </summary>
    public async Task<HandshakeResponse> StartHandshakeAsync(
        HandshakeRequest request,
        CancellationToken ct = default)
    {
        var workspaceId = $"ws-{Guid.CreateVersion7():N}"[..24];
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;

        var workspace = new WorkspaceRecord(
            workspaceId,
            request.ServiceName,
            request.ServiceName,
            request.SdkVersion,
            request.RuntimeVersion,
            request.Framework,
            request.GitCommit,
            "pending",
            now,
            now);

        await store.UpsertWorkspaceAsync(workspace, ct).ConfigureAwait(false);

        // Store PKCE challenge alongside the handshake session
        if (request.CodeChallenge is not null)
        {
            await store.UpsertHandshakeChallengeAsync(workspaceId, request.CodeChallenge, now, ct)
                .ConfigureAwait(false);
        }

        LogHandshakeStarted(workspaceId, request.ServiceName);
        return new HandshakeResponse(workspaceId, "pending");
    }

    /// <summary>
    ///     Verifies the handshake using a PKCE code_verifier, transitions state to verified,
    ///     and generates a bearer token for the workspace.
    /// </summary>
    /// <returns>Token string on success; null if the workspace is not found or PKCE fails.</returns>
    public async Task<HandshakeVerifyResult?> VerifyHandshakeAsync(
        string workspaceId,
        string codeVerifier,
        CancellationToken ct = default)
    {
        var existing = await store.GetWorkspaceAsync(workspaceId, ct).ConfigureAwait(false);
        if (existing is null || existing.Status != "pending")
            return null;

        // Validate PKCE: SHA256(code_verifier) must match stored code_challenge
        var challenge = await store.GetHandshakeChallengeAsync(workspaceId, ct).ConfigureAwait(false);
        if (challenge is null)
            return null;

        // Check expiry
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        if (now - challenge.CreatedAt > HandshakeExpiry)
        {
            await TransitionToExpiredAsync(workspaceId, ct).ConfigureAwait(false);
            LogHandshakeExpired(workspaceId);
            return null;
        }

        if (!ValidatePkce(codeVerifier, challenge.CodeChallenge))
        {
            LogPkceValidationFailed(workspaceId);
            return null;
        }

        // Transition to verified and generate token
        var token = GenerateToken();
        var updated = existing with { Status = "active" };
        await store.UpsertWorkspaceAsync(updated, ct).ConfigureAwait(false);
        await store.DeleteHandshakeChallengeAsync(workspaceId, ct).ConfigureAwait(false);

        LogHandshakeVerified(workspaceId);
        return new HandshakeVerifyResult(workspaceId, token, "active");
    }

    /// <summary>
    ///     Completes a handshake by marking the workspace as active (non-PKCE flow).
    /// </summary>
    public async Task<HandshakeResponse?> CompleteHandshakeAsync(
        string workspaceId,
        CancellationToken ct = default)
    {
        var existing = await store.GetWorkspaceAsync(workspaceId, ct).ConfigureAwait(false);
        if (existing is null)
            return null;

        var updated = existing with { Status = "active" };
        await store.UpsertWorkspaceAsync(updated, ct).ConfigureAwait(false);

        LogHandshakeCompleted(workspaceId);
        return new HandshakeResponse(workspaceId, "active");
    }

    /// <summary>
    ///     Expires a pending handshake session.
    /// </summary>
    public async Task<bool> ExpireHandshakeAsync(
        string workspaceId,
        CancellationToken ct = default)
    {
        var existing = await store.GetWorkspaceAsync(workspaceId, ct).ConfigureAwait(false);
        if (existing is null || existing.Status != "pending")
            return false;

        await TransitionToExpiredAsync(workspaceId, ct).ConfigureAwait(false);
        LogHandshakeExpired(workspaceId);
        return true;
    }

    // ==========================================================================
    // PKCE Helpers
    // ==========================================================================

    /// <summary>
    ///     Validates that SHA256(code_verifier) base64url-encoded matches the stored code_challenge.
    /// </summary>
    private static bool ValidatePkce(string codeVerifier, string codeChallenge)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        var computed = Convert.ToBase64String(hash)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return string.Equals(computed, codeChallenge, StringComparison.Ordinal);
    }

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private async Task TransitionToExpiredAsync(string workspaceId, CancellationToken ct)
    {
        var workspace = await store.GetWorkspaceAsync(workspaceId, ct).ConfigureAwait(false);
        if (workspace is not null)
        {
            var expired = workspace with { Status = "expired" };
            await store.UpsertWorkspaceAsync(expired, ct).ConfigureAwait(false);
        }

        await store.DeleteHandshakeChallengeAsync(workspaceId, ct).ConfigureAwait(false);
    }

    // ==========================================================================
    // LoggerMessage -- structured, zero-allocation logging
    // ==========================================================================

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Handshake started: {WorkspaceId} for service {ServiceName}")]
    private partial void LogHandshakeStarted(string workspaceId, string serviceName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Handshake verified: {WorkspaceId}")]
    private partial void LogHandshakeVerified(string workspaceId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Handshake completed: {WorkspaceId}")]
    private partial void LogHandshakeCompleted(string workspaceId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Handshake expired: {WorkspaceId}")]
    private partial void LogHandshakeExpired(string workspaceId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "PKCE validation failed: {WorkspaceId}")]
    private partial void LogPkceValidationFailed(string workspaceId);
}

// =============================================================================
// Handshake Records
// =============================================================================

/// <summary>
///     Request to initiate a workspace handshake, optionally with PKCE challenge.
/// </summary>
public sealed record HandshakeRequest(
    string ServiceName,
    string? SdkVersion = null,
    string? RuntimeVersion = null,
    string? Framework = null,
    string? GitCommit = null,
    string? CodeChallenge = null);

/// <summary>
///     Response from a workspace handshake start or complete operation.
/// </summary>
public sealed record HandshakeResponse(string WorkspaceId, string Status);

/// <summary>
///     Result of a successful PKCE handshake verification.
/// </summary>
public sealed record HandshakeVerifyResult(string WorkspaceId, string Token, string Status);

/// <summary>
///     Stored PKCE challenge for a pending handshake session.
/// </summary>
public sealed record HandshakeChallengeRecord(
    string WorkspaceId,
    string CodeChallenge,
    DateTime CreatedAt);
