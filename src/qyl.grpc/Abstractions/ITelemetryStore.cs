namespace qyl.Grpc.Abstractions;

public interface ITelemetryStore<T> where T : class
{
    void Add(T item);
    IReadOnlyList<T> Query(TelemetryQuery query);
    void Clear();
    long Count { get; }
}

public sealed record TelemetryQuery(
    string? ServiceName = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Limit = 100,
    int Offset = 0,
    Dictionary<string, string>? Filters = null);
