namespace qyl.grpc.Streaming;

public sealed record TelemetryStreamMessage(
    TelemetryStreamType Type,
    object Payload,
    DateTimeOffset Timestamp);

public enum TelemetryStreamType
{
    Span = 1,
    Metric = 2,
    Log = 3,
    ServiceUpdate = 4
}

public sealed record StreamSubscription(
    string? ServiceFilter = null,
    bool IncludeTraces = true,
    bool IncludeMetrics = true,
    bool IncludeLogs = true);
