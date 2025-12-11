using System.Collections.Concurrent;
using qyl.grpc.Abstractions;
using qyl.grpc.Models;

namespace qyl.grpc.Stores;

public sealed class InMemoryTelemetryStore<T> : ITelemetryStore<T>
    where T : class
{
    private readonly ConcurrentQueue<T> _items = new();
    private readonly int _maxItems;
    private long _count;

    public InMemoryTelemetryStore(int maxItems = 10_000) =>
        _maxItems = maxItems;

    public void Add(T item)
    {
        _items.Enqueue(item);
        Interlocked.Increment(ref _count);

        while (_count > _maxItems && _items.TryDequeue(out _)) Interlocked.Decrement(ref _count);
    }

    public IReadOnlyList<T> Query(TelemetryQuery query)
    {
        IEnumerable<T> items = _items.AsEnumerable();

        if (query.ServiceName is not null) items = items.Where(i => GetServiceName(i) == query.ServiceName);

        if (query.From.HasValue) items = items.Where(i => GetTimestamp(i) >= query.From.Value);

        if (query.To.HasValue) items = items.Where(i => GetTimestamp(i) <= query.To.Value);

        return
        [
            .. items
                .Reverse()
                .Skip(query.Offset)
                .Take(query.Limit)
        ];
    }

    public void Clear()
    {
        while (_items.TryDequeue(out _)) Interlocked.Decrement(ref _count);
    }

    public long Count => Interlocked.Read(ref _count);

    private static string? GetServiceName(T item) =>
        item switch
        {
            SpanModel s => s.Resource.ServiceName,
            MetricModel m => m.Resource.ServiceName,
            LogModel l => l.Resource.ServiceName,
            _ => null
        };

    private static DateTimeOffset GetTimestamp(T item) =>
        item switch
        {
            SpanModel s => s.StartTime,
            MetricModel m => m.DataPoints.FirstOrDefault()?.Timestamp ?? DateTimeOffset.MinValue,
            LogModel l => l.Timestamp,
            _ => DateTimeOffset.MinValue
        };
}
