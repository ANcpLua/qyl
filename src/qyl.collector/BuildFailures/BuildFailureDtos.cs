namespace qyl.collector.BuildFailures;

public sealed record BuildFailureIngestRequest(
    string? Id,
    DateTimeOffset? Timestamp,
    string Target,
    int ExitCode,
    string? BinlogPath,
    string? ErrorSummary,
    string? PropertyIssuesJson,
    string? EnvReadsJson,
    string? CallStackJson,
    int? DurationMs);

public sealed record BuildFailureResponse(
    string Id,
    DateTimeOffset Timestamp,
    string Target,
    int ExitCode,
    string? BinlogPath,
    string? ErrorSummary,
    string? PropertyIssuesJson,
    string? EnvReadsJson,
    string? CallStackJson,
    int? DurationMs,
    DateTimeOffset? CreatedAt);
