using System.Text;
using Qyl.Collector.Primitives;

namespace Qyl.Collector.Realtime;

internal sealed class LiveLogDeduplicator
{
    private const int StackHashBufferBytes = 512;
    private readonly Dictionary<DedupKey, LinkedListNode<DedupBucket>> _buckets = [];
    private readonly LinkedList<DedupBucket> _lru = [];
    private readonly int _maxBuckets;
    private readonly int _maxSuppressed;
    private readonly TimeSpan _window;

    public LiveLogDeduplicator(TimeSpan window, int maxSuppressed = 100, int maxBuckets = 2_048)
    {
        if (window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(window), window, "Window must be positive.");
        }

        Guard.NotLessThan(maxSuppressed, 1);
        Guard.NotLessThan(maxBuckets, 1);

        _window = window;
        _maxSuppressed = maxSuppressed;
        _maxBuckets = maxBuckets;
    }

    public IReadOnlyList<DeduplicatedLiveLog> ProcessBatch(IEnumerable<LogStorageRow> orderedLogs)
    {
        var output = orderedLogs is IReadOnlyCollection<LogStorageRow> collection
            ? new List<DeduplicatedLiveLog>(collection.Count)
            : new List<DeduplicatedLiveLog>();

        foreach (var log in orderedLogs)
        {
            var timestamp = QylTimeConversions.UnixNanoToDateTime(log.TimeUnixNano);
            FlushExpired(timestamp, output);

            var key = BuildKey(log);
            if (_buckets.TryGetValue(key, out var node))
            {
                var bucket = node.Value;
                bucket.Count++;
                bucket.LastSeenUtc = timestamp;
                bucket.LastLog = log;
                _lru.Remove(node);
                _lru.AddLast(node);

                if (bucket.Count - 1 >= _maxSuppressed)
                {
                    EmitSummary(bucket, output);
                    RemoveBucket(node);
                }

                continue;
            }

            if (_buckets.Count >= _maxBuckets)
                EvictOldestBucket(output);

            var newNode = new LinkedListNode<DedupBucket>(new DedupBucket(key, log, timestamp));
            _lru.AddLast(newNode);
            _buckets[key] = newNode;
            output.Add(new DeduplicatedLiveLog(log));
        }

        return output;
    }

    public IReadOnlyList<DeduplicatedLiveLog> FlushExpired(DateTime utcNow)
    {
        var output = new List<DeduplicatedLiveLog>();
        FlushExpired(utcNow, output);
        return output;
    }

    private static DedupKey BuildKey(LogStorageRow log) =>
        new(
            HashText(log.SessionId),
            log.SessionId?.Length ?? 0,
            HashText(log.TraceId),
            log.TraceId?.Length ?? 0,
            HashText(log.SpanId),
            log.SpanId?.Length ?? 0,
            HashText(log.ServiceName ?? "unknown"),
            log.ServiceName?.Length ?? "unknown".Length,
            log.SeverityNumber,
            HashText(log.Body),
            log.Body?.Length ?? 0);

    private void EvictOldestBucket(ICollection<DeduplicatedLiveLog> output)
    {
        if (_lru.First is not { } oldest)
            return;

        EmitSummary(oldest.Value, output);
        RemoveBucket(oldest);
    }

    private static void EmitSummary(DedupBucket bucket, ICollection<DeduplicatedLiveLog> output)
    {
        var suppressed = bucket.Count - 1;
        if (suppressed <= 0)
            return;

        output.Add(new DeduplicatedLiveLog(bucket.LastLog, suppressed, true));
    }

    private void FlushExpired(DateTime utcNow, ICollection<DeduplicatedLiveLog> output)
    {
        if (_buckets.Count is 0)
            return;

        while (_lru.First is { } oldest)
        {
            var bucket = oldest.Value;
            if (utcNow - bucket.LastSeenUtc < _window)
                return;

            EmitSummary(bucket, output);
            RemoveBucket(oldest);
        }
    }

    private void RemoveBucket(LinkedListNode<DedupBucket> node)
    {
        _buckets.Remove(node.Value.Key);
        _lru.Remove(node);
    }

    private static ulong HashText(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;

        var maxByteCount = Encoding.UTF8.GetMaxByteCount(value.Length);
        byte[]? rented = null;
        Span<byte> buffer = maxByteCount <= StackHashBufferBytes
            ? stackalloc byte[StackHashBufferBytes]
            : rented = ArrayPool<byte>.Shared.Rent(maxByteCount);

        try
        {
            var byteCount = Encoding.UTF8.GetBytes(value.AsSpan(), buffer);
            return XxHash64.HashToUInt64(buffer[..byteCount]);
        }
        finally
        {
            if (rented is not null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private readonly record struct DedupKey(
        ulong SessionHash,
        int SessionLength,
        ulong TraceHash,
        int TraceLength,
        ulong SpanHash,
        int SpanLength,
        ulong ServiceHash,
        int ServiceLength,
        byte SeverityNumber,
        ulong BodyHash,
        int BodyLength);

    private sealed class DedupBucket(DedupKey key, LogStorageRow firstLog, DateTime firstSeenUtc)
    {
        public DedupKey Key { get; } = key;
        public int Count { get; set; } = 1;
        public DateTime LastSeenUtc { get; set; } = firstSeenUtc;
        public LogStorageRow LastLog { get; set; } = firstLog;
    }
}

internal sealed record DeduplicatedLiveLog(
    LogStorageRow Log,
    int RepeatCount = 1,
    bool IsDuplicateSummary = false
);
