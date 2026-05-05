namespace Qyl.Contracts.Loom;

public enum IssueStatus
{
    New,
    Acknowledged,
    Resolved,
    Regressed,
    Reopened
}

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

public sealed record IssueEvent(
    string EventId,
    string IssueId,
    string EventType,
    string? OldValue,
    string? NewValue,
    string? Reason,
    DateTime CreatedAt);
