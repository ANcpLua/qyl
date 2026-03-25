namespace Qyl.Contracts.Loom;

/// <summary>
///     Pluggable coding agent backends that can receive autofix analysis
///     and generate code changes.
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

public static class CodingAgentProviderNames
{
    public static string ToSlug(CodingAgentProvider provider) => provider switch
    {
        CodingAgentProvider.Loom => "Loom",
        CodingAgentProvider.Cursor => "cursor",
        CodingAgentProvider.GithubCopilot => "github_copilot",
        CodingAgentProvider.ClaudeCode => "claude_code",
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null)
    };

    public static string NormalizeSlug(string? value) =>
        TryParse(value, out var provider) ? ToSlug(provider) : "Loom";

    public static bool TryParse(string? value, out CodingAgentProvider provider)
    {
        provider = CodingAgentProvider.Loom;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim().Replace("-", "_", StringComparison.Ordinal)
            .Replace(" ", "_", StringComparison.Ordinal).ToUpperInvariant();
        switch (normalized)
        {
            case "LOOM":
                provider = CodingAgentProvider.Loom;
                return true;
            case "CURSOR":
                provider = CodingAgentProvider.Cursor;
                return true;
            case "GITHUB_COPILOT":
            case "GITHUBCOPILOT":
                provider = CodingAgentProvider.GithubCopilot;
                return true;
            case "CLAUDE_CODE":
            case "CLAUDECODE":
                provider = CodingAgentProvider.ClaudeCode;
                return true;
            default:
                return Enum.TryParse(value, true, out provider);
        }
    }
}

/// <summary>
///     Storage record for a coding agent run. Maps to the coding_agent_runs DuckDB table.
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
///     Organization-level Loom configuration. Stored in the loom_settings DuckDB table.
/// </summary>
public sealed record LoomSettingsRecord
{
    public required string Id { get; init; }
    public string DefaultCodingAgent { get; init; } = "Loom";
    public string? DefaultCodingAgentIntegrationId { get; init; }
    public string AutomationTuning { get; init; } = "medium";
    public DateTime UpdatedAt { get; init; }
}
