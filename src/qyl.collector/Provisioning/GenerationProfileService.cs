namespace qyl.collector.Provisioning;

/// <summary>
///     CRUD for generation profiles, selections, and generation job queue management.
///     Provides built-in profiles and workspace selection persistence via DuckDbStore.
/// </summary>
public sealed partial class GenerationProfileService(DuckDbStore store, ILogger<GenerationProfileService> logger)
{
    private static readonly FrozenDictionary<string, GenerationProfile> BuiltInProfiles =
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

    // ==========================================================================
    // Profile Operations
    // ==========================================================================

    /// <summary>
    ///     Returns all available generation profiles (built-in set).
    /// </summary>
    public ValueTask<IReadOnlyList<GenerationProfile>> GetProfilesAsync(CancellationToken ct = default)
    {
        IReadOnlyList<GenerationProfile> profiles = [.. BuiltInProfiles.Values];
        return ValueTask.FromResult(profiles);
    }

    /// <summary>
    ///     Gets a single profile by ID. Returns null if not found.
    /// </summary>
    public ValueTask<GenerationProfile?> GetProfileAsync(string profileId, CancellationToken ct = default)
    {
        BuiltInProfiles.TryGetValue(profileId, out var profile);
        return ValueTask.FromResult(profile);
    }

    // ==========================================================================
    // Selection Operations
    // ==========================================================================

    /// <summary>
    ///     Saves a workspace's selected generation profile with optional custom overrides.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the profile ID is unknown.</exception>
    public async Task SetSelectionAsync(
        GenerationSelectionRequest request,
        CancellationToken ct = default)
    {
        if (!BuiltInProfiles.ContainsKey(request.ProfileId))
            throw new ArgumentException($"Unknown profile: {request.ProfileId}");

        await store.UpsertConfigSelectionAsync(request.WorkspaceId, request.ProfileId, request.CustomOverrides, ct)
            .ConfigureAwait(false);

        LogSelectionUpdated(request.WorkspaceId, request.ProfileId);
    }

    /// <summary>
    ///     Gets the current generation profile selection for a workspace.
    /// </summary>
    public Task<ConfigSelectionRecord?> GetSelectionAsync(
        string workspaceId,
        CancellationToken ct = default) =>
        store.GetConfigSelectionAsync(workspaceId, ct);

    // ==========================================================================
    // Generation Job Queue Operations
    // ==========================================================================

    /// <summary>
    ///     Enqueues a new generation job and returns the tracking record.
    /// </summary>
    public async Task<GenerationJobRecord> EnqueueJobAsync(
        GenerationJobRequest request,
        CancellationToken ct = default)
    {
        if (!BuiltInProfiles.ContainsKey(request.ProfileId))
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

    /// <summary>
    ///     Gets the current status of a generation job.
    /// </summary>
    public Task<GenerationJobRecord?> GetJobAsync(
        string jobId,
        CancellationToken ct = default) =>
        store.GetGenerationJobAsync(jobId, ct);

    /// <summary>
    ///     Cancels a pending generation job. Returns false if the job was not found or not in pending state.
    /// </summary>
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

    /// <summary>
    ///     Lists generation jobs for a workspace, ordered by creation time descending.
    /// </summary>
    public Task<IReadOnlyList<GenerationJobRecord>> ListJobsAsync(
        string workspaceId,
        int limit = 50,
        CancellationToken ct = default) =>
        store.GetGenerationJobsByWorkspaceAsync(workspaceId, limit, ct);

    // ==========================================================================
    // LoggerMessage -- structured, zero-allocation logging
    // ==========================================================================

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

// =============================================================================
// Generation Profile Records
// =============================================================================

/// <summary>
///     Defines a generation profile with its active interceptors.
/// </summary>
public sealed record GenerationProfile(
    string Id,
    string Name,
    string Description,
    IReadOnlyList<string> Interceptors);

/// <summary>
///     Request to save a workspace's profile selection.
/// </summary>
public sealed record GenerationSelectionRequest(
    string WorkspaceId,
    string ProfileId,
    string? CustomOverrides = null);
