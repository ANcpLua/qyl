namespace qyl.collector.Autofix;

public sealed record AgentHandoffRecord
{
    public required string HandoffId { get; init; }
    public required string RunId { get; init; }
    public required string AgentType { get; init; }
    public required string Status { get; init; }
    public string? ContextJson { get; init; }
    public string? ResultJson { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime? AcceptedAt { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public DateTime? FailedAt { get; init; }
    public DateTime? TimeoutAt { get; init; }
    public DateTime? CreatedAt { get; init; }
}
