namespace Qyl.Contracts.Copilot;

/// <summary>
///     Provides formatted issue context for AIAgent sessions.
///     Implemented by <c>IssueContextBuilder</c> in <c>qyl.collector</c>.
/// </summary>
public interface IIssueContextSource
{
    Task<string> GetFormattedContextAsync(
        string issueId, string? userContext = null, CancellationToken ct = default);
}
