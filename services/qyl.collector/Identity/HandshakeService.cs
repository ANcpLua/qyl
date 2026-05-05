namespace Qyl.Collector.Identity;

[QylService(QylLifetime.Singleton)]
public sealed partial class HandshakeService(DuckDbStore store, ILogger<HandshakeService> logger)
{
    private static readonly TimeSpan s_handshakeExpiry = TimeSpan.FromMinutes(10);

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

        if (request.CodeChallenge is not null)
        {
            await store.UpsertHandshakeChallengeAsync(workspaceId, request.CodeChallenge, now, ct)
                .ConfigureAwait(false);
        }

        LogHandshakeStarted(workspaceId, request.ServiceName);
        return new HandshakeResponse(workspaceId, "pending");
    }

    public async Task<HandshakeVerifyResult?> VerifyHandshakeAsync(
        string workspaceId,
        string codeVerifier,
        CancellationToken ct = default)
    {
        var existing = await store.GetWorkspaceAsync(workspaceId, ct).ConfigureAwait(false);
        if (existing is null || existing.Status != "pending")
            return null;

        if (await store.GetHandshakeChallengeAsync(workspaceId, ct).ConfigureAwait(false) is not { } challenge)
            return null;

        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        if (now - challenge.CreatedAt > s_handshakeExpiry)
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

        var token = GenerateToken();
        var updated = existing with { Status = "active" };
        await store.UpsertWorkspaceAsync(updated, ct).ConfigureAwait(false);
        await store.DeleteHandshakeChallengeAsync(workspaceId, ct).ConfigureAwait(false);

        LogHandshakeVerified(workspaceId);
        return new HandshakeVerifyResult(workspaceId, token, "active");
    }

    public async Task<HandshakeResponse?> CompleteHandshakeAsync(
        string workspaceId,
        CancellationToken ct = default)
    {
        if (await store.GetWorkspaceAsync(workspaceId, ct).ConfigureAwait(false) is not { } existing)
            return null;

        var updated = existing with { Status = "active" };
        await store.UpsertWorkspaceAsync(updated, ct).ConfigureAwait(false);

        LogHandshakeCompleted(workspaceId);
        return new HandshakeResponse(workspaceId, "active");
    }

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


    private static bool ValidatePkce(string codeVerifier, string codeChallenge) =>
        Pkce.ValidateS256(codeVerifier, codeChallenge);

    private static string GenerateToken() => Base64Url.NewRandom(32);

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


public sealed record HandshakeRequest(
    string ServiceName,
    string? SdkVersion = null,
    string? RuntimeVersion = null,
    string? Framework = null,
    string? GitCommit = null,
    string? CodeChallenge = null);

public sealed record HandshakeResponse(string WorkspaceId, string Status);

public sealed record HandshakeVerifyResult(string WorkspaceId, string Token, string Status);

public sealed record HandshakeChallengeRecord(
    string WorkspaceId,
    string CodeChallenge,
    DateTime CreatedAt);
