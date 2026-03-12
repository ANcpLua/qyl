namespace Qyl.Contracts.Copilot;

/// <summary>
///     Provides formatted issue context for AI agents and Loom workflows.
/// </summary>
public interface IIssueContextSource
{
    /// <summary>
    ///     Returns a formatted context block for the specified issue.
    /// </summary>
    Task<string> GetFormattedContextAsync(
        string issueId,
        string? userContext = null,
        CancellationToken ct = default);
}
