namespace Qyl.Contracts.Loom;

/// <summary>
///     Issue lifecycle status for the legacy <c>errors</c> table.
/// </summary>
public enum IssueStatus
{
    New,
    Acknowledged,
    Resolved,
    Regressed,
    Reopened
}

/// <summary>
///     Summary projection of an error issue from the <c>errors</c> table.
/// </summary>
public sealed record IssueSummary
{
    public required string IssueId { get; init; }
    public required string Fingerprint { get; init; }
    public required string ErrorType { get; init; }
    public string? ErrorMessage { get; init; }
    public required IssueStatus Status { get; init; }
    public string? Owner { get; init; }
    public required int EventCount { get; init; }
    public required DateTime FirstSeen { get; init; }
    public required DateTime LastSeen { get; init; }
}

/// <summary>
///     A lifecycle event record from the <c>issue_events</c> table.
/// </summary>
public sealed record IssueEvent(
    string EventId,
    string IssueId,
    string EventType,
    string? OldValue,
    string? NewValue,
    string? Reason,
    DateTime CreatedAt);
