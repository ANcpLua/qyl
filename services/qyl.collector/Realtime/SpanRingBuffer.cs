namespace Qyl.Collector.Realtime;

public sealed class SpanRingBuffer
{
    private readonly SpanStorageRow?[] _buffer;
    private readonly Lock _lock = new();
    private int _count;
    private ulong _generation;

    private int _head;

    public SpanRingBuffer(int capacity = 10_000)
    {
        Guard.NotLessThan(capacity, 1);
        Capacity = capacity;
        _buffer = new SpanStorageRow?[capacity];
    }

    public int Capacity { get; }

    public int Count
    {
        get
        {
            lock (_lock) return _count;
        }
    }

    public ulong Generation => Volatile.Read(ref _generation);

    public void PushRange(IEnumerable<SpanStorageRow> spans)
    {
        Guard.NotNull(spans);
        IReadOnlyList<SpanStorageRow> materialized = spans as IReadOnlyList<SpanStorageRow> ?? [.. spans];
        lock (_lock)
        {
            foreach (var span in materialized)
            {
                _buffer[_head] = span;
                _head = (_head + 1) % Capacity;
                if (_count < Capacity) _count++;
            }

            Volatile.Write(ref _generation, _generation + 1);
        }
    }

    public SpanStorageRow[] GetLatest(int count, out ulong generation)
    {
        lock (_lock)
        {
            generation = _generation;
            var take = Math.Min(count, _count);
            if (take is 0) return [];
            var result = new SpanStorageRow[take];
            var idx = (_head - 1 + Capacity) % Capacity;
            for (var i = 0; i < take; i++)
            {
                if (_buffer[idx] is { } span)
                    result[i] = span;
                idx = (idx - 1 + Capacity) % Capacity;
            }

            return result;
        }
    }

    private SpanStorageRow[] Query(Func<SpanStorageRow, bool> predicate, int maxCount, out ulong generation)
    {
        lock (_lock)
        {
            generation = _generation;
            if (_count is 0) return [];
            var results = new List<SpanStorageRow>(Math.Min(maxCount, _count));
            var idx = (_head - 1 + Capacity) % Capacity;
            var scanned = 0;
            while (scanned < _count && results.Count < maxCount)
            {
                var span = _buffer[idx];
                if (span is not null && predicate(span)) results.Add(span);
                idx = (idx - 1 + Capacity) % Capacity;
                scanned++;
            }

            return [.. results];
        }
    }

    public SpanStorageRow[] GetByTraceId(string traceId, out ulong generation)
    {
        if (string.IsNullOrWhiteSpace(traceId))
        {
            generation = Generation;
            return [];
        }

        return Query(s => string.Equals(s.TraceId, traceId, StringComparison.Ordinal), Capacity, out generation);
    }

    public SpanStorageRow[] GetBySessionId(string sessionId, int maxCount, out ulong generation)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            generation = Generation;
            return [];
        }

        return Query(s => string.Equals(s.SessionId, sessionId, StringComparison.Ordinal), maxCount, out generation);
    }

    public void Clear()
    {
        lock (_lock)
        {
            Array.Clear(_buffer);
            _head = 0;
            _count = 0;
            Volatile.Write(ref _generation, _generation + 1);
        }
    }
}
