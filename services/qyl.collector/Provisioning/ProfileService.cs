namespace Qyl.Collector.Provisioning;

[QylService(QylLifetime.Singleton)]
public sealed partial class ProfileService(DuckDbStore store, ILogger<ProfileService> logger)
{
    private static readonly FrozenDictionary<string, InstrumentationProfile> s_builtInProfiles =
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

    public ValueTask<IReadOnlyList<InstrumentationProfile>> GetProfilesAsync(CancellationToken _ = default)
    {
        IReadOnlyList<InstrumentationProfile> profiles = [.. s_builtInProfiles.Values];
        return ValueTask.FromResult(profiles);
    }

    public async Task SetSelectionAsync(
        ConfigSelectionRequest request,
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


    [LoggerMessage(Level = LogLevel.Information,
        Message = "Config selection updated: workspace {WorkspaceId} → profile {ProfileId}")]
    private partial void LogSelectionUpdated(string workspaceId, string profileId);
}


public sealed record InstrumentationProfile(
    string Id,
    string Name,
    string Description,
    IReadOnlyList<string> Interceptors);

public sealed record ConfigSelectionRequest(
    string WorkspaceId,
    string ProfileId,
    string? CustomOverrides = null);

public sealed record ConfigSelectionRecord(
    string WorkspaceId,
    string ProfileId,
    string? CustomOverrides,
    DateTime UpdatedAt);

public sealed record GenerationJobRequest(
    string WorkspaceId,
    string ProfileId);

public sealed record GenerationJobRecord(
    string JobId,
    string? WorkspaceId,
    string? ProfileId,
    string Status,
    string? OutputUrl,
    string? ErrorMessage,
    DateTime CreatedAt,
    DateTime? CompletedAt);
