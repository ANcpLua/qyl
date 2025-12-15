namespace qyl.collector.Storage;

public sealed record SpanRecord
{
    public required string TraceId { get; init; }
    public required string SpanId { get; init; }
    public string? ParentSpanId { get; init; }
    public string? SessionId { get; init; }
    public required string Name { get; init; }
    public string? Kind { get; init; }
    public required DateTime StartTime { get; init; }
    public required DateTime EndTime { get; init; }
    public int? StatusCode { get; init; }
    public string? StatusMessage { get; init; }
    public string? ProviderName { get; init; }
    public string? RequestModel { get; init; }
    public int? TokensIn { get; init; }
    public int? TokensOut { get; init; }
    public decimal? CostUsd { get; init; }
    public float? EvalScore { get; init; }
    public string? EvalReason { get; init; }
    public string? Attributes { get; init; }
    public string? Events { get; init; }
}
