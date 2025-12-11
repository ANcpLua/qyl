namespace qyl.grpc.Abstractions;

public interface ITelemetryStore<T>
    where T : class
{
    long Count { get; }
    void Add(T item);
    IReadOnlyList<T> Query(TelemetryQuery query);
    void Clear();
}

public sealed record TelemetryQuery(
    string? ServiceName = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Limit = 100,
    int Offset = 0,
    Dictionary<string, string>? Filters = null);
