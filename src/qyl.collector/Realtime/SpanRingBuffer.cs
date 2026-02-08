namespace qyl.collector.Realtime;

/// <summary>
///     Thread-safe circular buffer for real-time span queries.
///     O(1) push with pre-allocated storage and generation tracking for cache invalidation.
/// </summary>
public sealed class SpanRingBuffer
{
    private readonly SpanRecord?[] _buffer;
    private readonly Lock _lock = new();
    private int _count;
    private ulong _generation;

    private int _head;

    public SpanRingBuffer(int capacity = 10_000)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        Capacity = capacity;
        _buffer = new SpanRecord?[capacity];
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

    public void Push(SpanRecord span)
    {
        Throw.IfNull(span);
        lock (_lock)
        {
            _buffer[_head] = span;
            _head = (_head + 1) % Capacity;
            if (_count < Capacity) _count++;
            Volatile.Write(ref _generation, _generation + 1);
        }
    }

    public void PushRange(IEnumerable<SpanRecord> spans)
    {
        ArgumentNullException.ThrowIfNull(spans);
        // Materialize once to avoid multiple enumeration of IEnumerable
        var materialized = spans as IReadOnlyList<SpanRecord> ?? [.. spans];
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

    public SpanRecord[] GetLatest(int count, out ulong generation)
    {
        lock (_lock)
        {
            generation = _generation;
            var take = Math.Min(count, _count);
            if (take is 0) return [];
            var result = new SpanRecord[take];
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

    public SpanRecord[] Query(Func<SpanRecord, bool> predicate, int maxCount, out ulong generation)
    {
        lock (_lock)
        {
            generation = _generation;
            if (_count is 0) return [];
            var results = new List<SpanRecord>(Math.Min(maxCount, _count));
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

    public SpanRecord[] GetByTraceId(string traceId, out ulong generation)
    {
        if (!TraceId.TryParse(traceId, null, out var tid))
        {
            generation = Generation;
            return [];
        }

        return Query(s => s.TraceId == tid, Capacity, out generation);
    }

    public SpanRecord[] GetBySessionId(string sessionId, int maxCount, out ulong generation)
    {
        var sid = new SessionId(sessionId);
        if (!sid.IsValid)
        {
            generation = Generation;
            return [];
        }

        return Query(s => s.SessionId == sid, maxCount, out generation);
    }

    public SpanRecord[] GetAllOldestFirst(out ulong generation)
    {
        lock (_lock)
        {
            generation = _generation;
            if (_count is 0) return [];
            var result = new SpanRecord[_count];
            var startIdx = _count < Capacity ? 0 : _head;
            for (var i = 0; i < _count; i++)
            {
                var idx = (startIdx + i) % Capacity;
                if (_buffer[idx] is { } span)
                    result[i] = span;
            }

            return result;
        }
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
