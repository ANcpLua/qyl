namespace qyl.collector.Storage;

public sealed record BuildFailureRecord
{
    public required string Id { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required string Target { get; init; }
    public required int ExitCode { get; init; }
    public string? BinlogPath { get; init; }
    public string? ErrorSummary { get; init; }
    public string? PropertyIssuesJson { get; init; }
    public string? EnvReadsJson { get; init; }
    public string? CallStackJson { get; init; }
    public int? DurationMs { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
}
