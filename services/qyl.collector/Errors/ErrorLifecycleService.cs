namespace Qyl.Collector.Errors;

[QylService(QylLifetime.Singleton)]
public sealed partial class ErrorLifecycleService(DuckDbStore store, ILogger<ErrorLifecycleService> logger)
{
    private static readonly FrozenDictionary<IssueStatus, IssueStatus[]> s_allowedTransitions =
        new Dictionary<IssueStatus, IssueStatus[]>
        {
            [IssueStatus.New] = [IssueStatus.Acknowledged, IssueStatus.Resolved],
            [IssueStatus.Acknowledged] = [IssueStatus.Resolved, IssueStatus.Reopened],
            [IssueStatus.Resolved] = [IssueStatus.Reopened, IssueStatus.Regressed],
            [IssueStatus.Regressed] = [IssueStatus.Acknowledged, IssueStatus.Resolved],
            [IssueStatus.Reopened] = [IssueStatus.Acknowledged, IssueStatus.Resolved]
        }.ToFrozenDictionary();

    public async Task<bool> TransitionStatusAsync(string issueId, IssueStatus newStatus, string? reason = null,
        CancellationToken ct = default)
    {
        if (await store.GetErrorByIdAsync(issueId, ct).ConfigureAwait(false) is not { } existing)
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

    public static bool IsTransitionAllowed(IssueStatus from, IssueStatus to) =>
        s_allowedTransitions.TryGetValue(from, out var allowed) && allowed.AsSpan().Contains(to);

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
