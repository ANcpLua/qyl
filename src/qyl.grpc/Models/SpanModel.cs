namespace qyl.Grpc.Models;

public sealed record SpanModel(
    string TraceId,
    string SpanId,
    string? ParentSpanId,
    string Name,
    SpanKind Kind,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    SpanStatus Status,
    string? StatusMessage,
    IReadOnlyDictionary<string, AttributeValue> Attributes,
    IReadOnlyList<SpanEvent> Events,
    IReadOnlyList<SpanLink> Links,
    ResourceModel Resource)
{
    public long DurationNs => (EndTime - StartTime).Ticks * 100;
    public double DurationMs => DurationNs / 1_000_000.0;
}

public enum SpanKind
{
    Unspecified = 0,
    Internal = 1,
    Server = 2,
    Client = 3,
    Producer = 4,
    Consumer = 5
}

public enum SpanStatus
{
    Unset = 0,
    Ok = 1,
    Error = 2
}

public sealed record SpanEvent(
    string Name,
    DateTimeOffset Timestamp,
    IReadOnlyDictionary<string, AttributeValue> Attributes);

public sealed record SpanLink(
    string TraceId,
    string SpanId,
    IReadOnlyDictionary<string, AttributeValue> Attributes);
