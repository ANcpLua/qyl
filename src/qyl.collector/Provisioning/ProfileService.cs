namespace qyl.collector.Provisioning;

/// <summary>
///     Business logic for instrumentation profile management.
///     Provides built-in profiles and workspace selection persistence.
/// </summary>
public sealed partial class ProfileService(DuckDbStore store, ILogger<ProfileService> logger)
{
    private static readonly FrozenDictionary<string, InstrumentationProfile> BuiltInProfiles =
        new Dictionary<string, InstrumentationProfile>
        {
            ["full"] = new("full", "Full Observability",
                "Traces, logs, metrics, GenAI, errors, and alerts",
                ["traces", "logs", "metrics", "genai", "errors", "alerts"]),
            ["minimal"] = new("minimal", "Minimal",
                "Traces only — lightweight baseline observability",
                ["traces"]),
            ["genai"] = new("genai", "GenAI + Agents",
                "GenAI provider spans, agent runs, and tool calls",
                ["traces", "genai"]),
            ["errors"] = new("errors", "Errors + Alerts",
                "Error tracking with alerting rules",
                ["errors", "alerts"])
        }.ToFrozenDictionary();

    /// <summary>
    ///     Returns all available instrumentation profiles.
    /// </summary>
    public Task<IReadOnlyList<InstrumentationProfile>> GetProfilesAsync(CancellationToken ct = default)
    {
        IReadOnlyList<InstrumentationProfile> profiles = [.. BuiltInProfiles.Values];
        return Task.FromResult(profiles);
    }

    /// <summary>
    ///     Saves a workspace's selected instrumentation profile.
    /// </summary>
    public async Task SetSelectionAsync(
        ConfigSelectionRequest request,
        CancellationToken ct = default)
    {
        if (!BuiltInProfiles.ContainsKey(request.ProfileId))
            throw new ArgumentException($"Unknown profile: {request.ProfileId}");

        await store.UpsertConfigSelectionAsync(request.WorkspaceId, request.ProfileId, request.CustomOverrides, ct)
            .ConfigureAwait(false);

        LogSelectionUpdated(request.WorkspaceId, request.ProfileId);
    }

    /// <summary>
    ///     Gets the current instrumentation profile selection for a workspace.
    /// </summary>
    public Task<ConfigSelectionRecord?> GetSelectionAsync(
        string workspaceId,
        CancellationToken ct = default) =>
        store.GetConfigSelectionAsync(workspaceId, ct);

    // ==========================================================================
    // LoggerMessage — structured, zero-allocation logging
    // ==========================================================================

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Config selection updated: workspace {WorkspaceId} → profile {ProfileId}")]
    private partial void LogSelectionUpdated(string workspaceId, string profileId);
}

// =============================================================================
// Provisioning Records
// =============================================================================

/// <summary>
///     Defines an instrumentation profile with its active interceptors.
/// </summary>
public sealed record InstrumentationProfile(
    string Id,
    string Name,
    string Description,
    IReadOnlyList<string> Interceptors);

/// <summary>
///     Request to save a workspace's profile selection.
/// </summary>
public sealed record ConfigSelectionRequest(
    string WorkspaceId,
    string ProfileId,
    string? CustomOverrides = null);

/// <summary>
///     Storage record for a workspace's profile selection.
/// </summary>
public sealed record ConfigSelectionRecord(
    string WorkspaceId,
    string ProfileId,
    string? CustomOverrides,
    DateTime UpdatedAt);

/// <summary>
///     Request to start a code generation job for a selected profile.
/// </summary>
public sealed record GenerationJobRequest(
    string WorkspaceId,
    string ProfileId);

/// <summary>
///     Storage record for a code generation job.
/// </summary>
public sealed record GenerationJobRecord(
    string JobId,
    string? WorkspaceId,
    string? ProfileId,
    string Status,
    string? OutputUrl,
    string? ErrorMessage,
    DateTime CreatedAt,
    DateTime? CompletedAt);
