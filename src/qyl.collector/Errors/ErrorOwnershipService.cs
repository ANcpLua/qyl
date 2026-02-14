namespace qyl.collector.Errors;

/// <summary>
///     Manages issue ownership — assignment of issues to services, teams, or individuals.
/// </summary>
public sealed partial class ErrorOwnershipService(DuckDbStore store, ILogger<ErrorOwnershipService> logger)
{
    /// <summary>
    ///     Assigns an owner to an issue. Owner can be a service name, team, or individual.
    /// </summary>
    /// <returns><c>true</c> if the issue was found and assigned; <c>false</c> otherwise.</returns>
    public async Task<bool> AssignOwnerAsync(string issueId, string owner, CancellationToken ct = default)
    {
        var existing = await store.GetErrorByIdAsync(issueId, ct).ConfigureAwait(false);
        if (existing is null)
            return false;

        await store.AssignIssueOwnerAsync(issueId, owner, ct).ConfigureAwait(false);
        LogOwnerAssigned(issueId, owner);
        return true;
    }

    /// <summary>
    ///     Gets the current owner of an issue. Returns null if unassigned or not found.
    /// </summary>
    public async Task<string?> GetOwnerAsync(string issueId, CancellationToken ct = default) =>
        await store.GetIssueOwnerAsync(issueId, ct).ConfigureAwait(false);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Issue {IssueId} assigned to {Owner}")]
    private partial void LogOwnerAssigned(string issueId, string owner);
}
