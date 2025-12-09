namespace qyl.Grpc.Abstractions;

public interface ITelemetryBroadcaster
{
    ValueTask BroadcastAsync<T>(TelemetrySignal signal, T data, CancellationToken ct = default);
    IAsyncEnumerable<TelemetryMessage> SubscribeAsync(CancellationToken ct);
    int ConnectionCount { get; }
}

public enum TelemetrySignal { Trace, Metric, Log }

public sealed record TelemetryMessage(
    TelemetrySignal Signal,
    object Data,
    DateTimeOffset Timestamp);
