namespace qyl.collector.Errors;

/// <summary>
///     Issue lifecycle state machine. Validates and applies status transitions:
///     <c>new → acknowledged → resolved → regressed</c>,
///     <c>resolved ↔ reopened</c>.
/// </summary>
public sealed partial class ErrorLifecycleService(DuckDbStore store, ILogger<ErrorLifecycleService> logger)
{
    /// <summary>Valid transitions from each source status.</summary>
    private static readonly FrozenDictionary<IssueStatus, IssueStatus[]> AllowedTransitions =
        new Dictionary<IssueStatus, IssueStatus[]>
        {
            [IssueStatus.New] = [IssueStatus.Acknowledged, IssueStatus.Resolved],
            [IssueStatus.Acknowledged] = [IssueStatus.Resolved, IssueStatus.Reopened],
            [IssueStatus.Resolved] = [IssueStatus.Reopened, IssueStatus.Regressed],
            [IssueStatus.Regressed] = [IssueStatus.Acknowledged, IssueStatus.Resolved],
            [IssueStatus.Reopened] = [IssueStatus.Acknowledged, IssueStatus.Resolved]
        }.ToFrozenDictionary();

    /// <summary>
    ///     Transitions the status of an issue, validating the transition is allowed.
    /// </summary>
    /// <returns><c>true</c> if the transition succeeded; <c>false</c> if the issue was not found.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the transition is invalid.</exception>
    public async Task<bool> TransitionStatusAsync(string issueId, IssueStatus newStatus, string? reason = null,
        CancellationToken ct = default)
    {
        var existing = await store.GetErrorByIdAsync(issueId, ct).ConfigureAwait(false);
        if (existing is null)
            return false;

        var currentStatus = ParseStatus(existing.Status);
        if (!IsTransitionAllowed(currentStatus, newStatus))
        {
            throw new InvalidOperationException(
                $"Cannot transition from '{currentStatus}' to '{newStatus}'.");
        }

        var statusString = StatusToString(newStatus);
        await store.UpdateIssueStatusAsync(issueId, statusString, reason, ct).ConfigureAwait(false);

        LogStatusTransition(issueId, currentStatus.ToString(), newStatus.ToString(), reason);
        return true;
    }

    /// <summary>
    ///     Checks whether a transition from <paramref name="from" /> to <paramref name="to" /> is valid.
    /// </summary>
    public static bool IsTransitionAllowed(IssueStatus from, IssueStatus to) =>
        AllowedTransitions.TryGetValue(from, out var allowed) && allowed.AsSpan().Contains(to);

    private static IssueStatus ParseStatus(string status) =>
        status switch
        {
            "new" => IssueStatus.New,
            "acknowledged" => IssueStatus.Acknowledged,
            "resolved" => IssueStatus.Resolved,
            "regressed" => IssueStatus.Regressed,
            "reopened" => IssueStatus.Reopened,
            _ => IssueStatus.New
        };

    internal static string StatusToString(IssueStatus status) =>
        status switch
        {
            IssueStatus.New => "new",
            IssueStatus.Acknowledged => "acknowledged",
            IssueStatus.Resolved => "resolved",
            IssueStatus.Regressed => "regressed",
            IssueStatus.Reopened => "reopened",
            _ => "new"
        };

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Issue {IssueId} transitioned: {From} → {To} (reason: {Reason})")]
    private partial void LogStatusTransition(string issueId, string from, string to, string? reason);
}
