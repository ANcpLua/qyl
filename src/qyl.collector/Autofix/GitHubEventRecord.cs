namespace qyl.collector.Autofix;

/// <summary>
///     Storage record for a GitHub webhook event. Maps to the github_events DuckDB table.
/// </summary>
public sealed record GitHubEventRecord
{
    public required string EventId { get; init; }
    public required string EventType { get; init; }
    public string? Action { get; init; }
    public required string RepoFullName { get; init; }
    public string? Sender { get; init; }
    public int? PrNumber { get; init; }
    public string? PrUrl { get; init; }
    public string? Ref { get; init; }
    public string? PayloadJson { get; init; }
    public DateTime? CreatedAt { get; init; }
}
