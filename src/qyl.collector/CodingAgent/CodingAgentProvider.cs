namespace qyl.collector.CodingAgent;

/// <summary>
///     Pluggable coding agent backends that can receive autofix analysis
///     and generate code changes. Mirrors Sentry's provider model.
/// </summary>
public enum CodingAgentProvider
{
    /// <summary>Built-in Loom autofix pipeline (default).</summary>
    Loom,

    /// <summary>Cursor Background Agent API.</summary>
    Cursor,

    /// <summary>GitHub Copilot Coding Agent Tasks API.</summary>
    GithubCopilot,

    /// <summary>Anthropic Claude Code agent API (experimental).</summary>
    ClaudeCode
}

/// <summary>
///     Storage record for a coding agent run. Maps to the coding_agent_runs DuckDB table.
///     A fix run can spawn multiple coding agent runs (one per repo).
/// </summary>
public sealed record CodingAgentRunRecord
{
    public required string Id { get; init; }
    public required string FixRunId { get; init; }
    public required string Provider { get; init; }
    public required string Status { get; init; }
    public string? AgentUrl { get; init; }
    public string? PrUrl { get; init; }
    public string? RepoFullName { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}

/// <summary>
///     Organization-level Loom configuration. Stored in the Loom_settings DuckDB table.
/// </summary>
public sealed record LoomSettingsRecord
{
    public required string Id { get; init; }
    public string DefaultCodingAgent { get; init; } = "Loom";
    public string? DefaultCodingAgentIntegrationId { get; init; }
    public string AutomationTuning { get; init; } = "medium";
    public DateTime UpdatedAt { get; init; }
}
