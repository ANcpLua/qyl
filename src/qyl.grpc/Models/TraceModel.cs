namespace qyl.Grpc.Models;

public sealed record TraceModel(
    string TraceId,
    IReadOnlyList<SpanModel> Spans,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    TraceStatus Status,
    IReadOnlySet<string> Services)
{
    public long DurationNs => (EndTime - StartTime).Ticks * 100;
    public double DurationMs => DurationNs / 1_000_000.0;

    public string RootServiceName => Spans
        .FirstOrDefault(s => s.ParentSpanId is null)?.Resource.ServiceName ?? "unknown";

    public string RootSpanName => Spans
        .FirstOrDefault(s => s.ParentSpanId is null)?.Name ?? "unknown";

    public int SpanCount => Spans.Count;

    public int ErrorCount => Spans.Count(s => s.Status == SpanStatus.Error);
}

public enum TraceStatus
{
    Ok = 0,
    Error = 1
}
