namespace qyl.collector.Errors;

public sealed record ErrorEvent
{
    public required string ErrorType { get; init; }
    public required string Message { get; init; }
    public required string Category { get; init; }
    public required string Fingerprint { get; init; }
    public required string ServiceName { get; init; }
    public required string TraceId { get; init; }
    public string? UserId { get; init; }
    public string? GenAiProvider { get; init; }
    public string? GenAiModel { get; init; }
    public string? GenAiOperation { get; init; }
}

public sealed record ErrorRow
{
    public required string ErrorId { get; init; }
    public required string ErrorType { get; init; }
    public required string Message { get; init; }
    public required string Category { get; init; }
    public required string Fingerprint { get; init; }
    public required DateTimeOffset FirstSeen { get; init; }
    public required DateTimeOffset LastSeen { get; init; }
    public required long OccurrenceCount { get; init; }
    public string? AffectedUserIds { get; init; }
    public string? AffectedServices { get; init; }
    public required string Status { get; init; }
    public string? AssignedTo { get; init; }
    public string? IssueUrl { get; init; }
    public string? SampleTraces { get; init; }
}

public sealed record ErrorStats
{
    public required long TotalCount { get; init; }
    public required IReadOnlyList<ErrorCategoryStat> ByCategory { get; init; }
}

public sealed record ErrorCategoryStat
{
    public required string Category { get; init; }
    public required long Count { get; init; }
}
