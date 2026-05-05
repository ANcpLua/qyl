namespace Qyl.Collector.Provisioning;

[QylService(QylLifetime.Singleton)]
public sealed partial class GenerationProfileService(DuckDbStore store, ILogger<GenerationProfileService> logger)
{
    private static readonly FrozenDictionary<string, GenerationProfile> s_builtInProfiles =
        new Dictionary<string, GenerationProfile>
        {
            ["full"] = new("full", "Full Observability",
                "Traces, logs, metrics, GenAI, errors, and alerts",
                ["traces", "logs", "metrics", "genai", "errors", "alerts"]),
            ["minimal"] = new("minimal", "Minimal",
                "Traces only -- lightweight baseline observability",
                ["traces"]),
            ["genai"] = new("genai", "GenAI + Agents",
                "GenAI provider spans, agent runs, and tool calls",
                ["traces", "genai"]),
            ["errors"] = new("errors", "Errors + Alerts",
                "Error tracking with alerting rules",
                ["errors", "alerts"])
        }.ToFrozenDictionary();


    public ValueTask<IReadOnlyList<GenerationProfile>> GetProfilesAsync(CancellationToken _ = default)
    {
        IReadOnlyList<GenerationProfile> profiles = [.. s_builtInProfiles.Values];
        return ValueTask.FromResult(profiles);
    }

    public ValueTask<GenerationProfile?> GetProfileAsync(string profileId, CancellationToken _ = default)
    {
        s_builtInProfiles.TryGetValue(profileId, out var profile);
        return ValueTask.FromResult(profile);
    }


    public async Task SetSelectionAsync(
        GenerationSelectionRequest request,
        CancellationToken ct = default)
    {
        if (!s_builtInProfiles.ContainsKey(request.ProfileId))
            throw new ArgumentException($"Unknown profile: {request.ProfileId}");

        await store.UpsertConfigSelectionAsync(request.WorkspaceId, request.ProfileId, request.CustomOverrides, ct)
            .ConfigureAwait(false);

        LogSelectionUpdated(request.WorkspaceId, request.ProfileId);
    }

    public Task<ConfigSelectionRecord?> GetSelectionAsync(
        string workspaceId,
        CancellationToken ct = default) =>
        store.GetConfigSelectionAsync(workspaceId, ct);


    public async Task<GenerationJobRecord> EnqueueJobAsync(
        GenerationJobRequest request,
        CancellationToken ct = default)
    {
        if (!s_builtInProfiles.ContainsKey(request.ProfileId))
            throw new ArgumentException($"Unknown profile: {request.ProfileId}");

        var jobId = $"gen-{Guid.CreateVersion7():N}"[..24];
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;

        var job = new GenerationJobRecord(
            jobId,
            request.WorkspaceId,
            request.ProfileId,
            "pending",
            null,
            null,
            now,
            null);

        await store.InsertGenerationJobAsync(job, ct).ConfigureAwait(false);
        LogJobEnqueued(jobId, request.ProfileId);
        return job;
    }

    public Task<GenerationJobRecord?> GetJobAsync(
        string jobId,
        CancellationToken ct = default) =>
        store.GetGenerationJobAsync(jobId, ct);

    public async Task<bool> CancelJobAsync(
        string jobId,
        CancellationToken ct = default)
    {
        var existing = await store.GetGenerationJobAsync(jobId, ct).ConfigureAwait(false);
        if (existing is null || existing.Status != "pending")
            return false;

        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var cancelled = existing with { Status = "cancelled", CompletedAt = now };
        await store.UpdateGenerationJobAsync(cancelled, ct).ConfigureAwait(false);

        LogJobCancelled(jobId);
        return true;
    }

    public Task<IReadOnlyList<GenerationJobRecord>> ListJobsAsync(
        string workspaceId,
        int limit = 50,
        CancellationToken ct = default) =>
        store.GetGenerationJobsByWorkspaceAsync(workspaceId, limit, ct);


    [LoggerMessage(Level = LogLevel.Information,
        Message = "Config selection updated: workspace {WorkspaceId} -> profile {ProfileId}")]
    private partial void LogSelectionUpdated(string workspaceId, string profileId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Generation job enqueued: {JobId} for profile {ProfileId}")]
    private partial void LogJobEnqueued(string jobId, string profileId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Generation job cancelled: {JobId}")]
    private partial void LogJobCancelled(string jobId);
}


public sealed record GenerationProfile(
    string Id,
    string Name,
    string Description,
    IReadOnlyList<string> Interceptors);

public sealed record GenerationSelectionRequest(
    string WorkspaceId,
    string ProfileId,
    string? CustomOverrides = null);
