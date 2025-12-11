namespace qyl.grpc.Abstractions;

public interface ITelemetryBroadcaster
{
    int ConnectionCount { get; }
    ValueTask BroadcastAsync<T>(TelemetrySignal signal, T data, CancellationToken ct = default);
    IAsyncEnumerable<TelemetryMessage> SubscribeAsync(CancellationToken ct);
}

public enum TelemetrySignal
{
    Trace,
    Metric,
    Log
}

public sealed record TelemetryMessage(
    TelemetrySignal Signal,
    object Data,
    DateTimeOffset Timestamp);
