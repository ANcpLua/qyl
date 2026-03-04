namespace qyl.collector.Realtime;

/// <summary>
///     Quern-inspired live log deduplicator.
///     Emits the first log immediately, suppresses repeated identical logs
///     inside a sliding window, then emits a summary event once the burst quiets.
/// </summary>
internal sealed class LiveLogDeduplicator
{
    private readonly TimeSpan _window;
    private readonly int _maxSuppressed;
    private readonly Dictionary<string, DedupBucket> _buckets = new(StringComparer.Ordinal);

    public LiveLogDeduplicator(TimeSpan window, int maxSuppressed = 100)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(window, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxSuppressed, 1);

        _window = window;
        _maxSuppressed = maxSuppressed;
    }

    /// <summary>
    ///     Processes logs in ascending timestamp order.
    ///     First occurrences are emitted immediately; duplicate bursts are summarized later.
    /// </summary>
    public IReadOnlyList<DeduplicatedLiveLog> ProcessBatch(IEnumerable<LogStorageRow> orderedLogs)
    {
        var output = new List<DeduplicatedLiveLog>();

        foreach (var log in orderedLogs)
        {
            var timestamp = TimeConversions.UnixNanoToDateTime(log.TimeUnixNano);
            FlushExpired(timestamp, output);

            var key = BuildKey(log);
            if (_buckets.TryGetValue(key, out var bucket))
            {
                bucket.Count++;
                bucket.LastSeenUtc = timestamp;
                bucket.LastLog = log;

                // Prevent unbounded suppression during sustained spam bursts.
                if (bucket.Count - 1 >= _maxSuppressed)
                {
                    EmitSummary(bucket, output);
                    _buckets.Remove(key);
                }

                continue;
            }

            _buckets[key] = new DedupBucket(log, timestamp);
            output.Add(new DeduplicatedLiveLog(log));
        }

        return output;
    }

    /// <summary>
    ///     Flushes only buckets whose dedupe windows have expired.
    /// </summary>
    public IReadOnlyList<DeduplicatedLiveLog> FlushExpired(DateTime utcNow)
    {
        var output = new List<DeduplicatedLiveLog>();
        FlushExpired(utcNow, output);
        return output;
    }

    /// <summary>
    ///     Flushes all pending buckets (used when the caller needs final summaries).
    /// </summary>
    public IReadOnlyList<DeduplicatedLiveLog> FlushAll()
    {
        var output = new List<DeduplicatedLiveLog>(_buckets.Count);
        var keys = _buckets.Keys.ToArray();

        foreach (var key in keys)
        {
            var bucket = _buckets[key];
            EmitSummary(bucket, output);
            _buckets.Remove(key);
        }

        return output;
    }

    private static string BuildKey(LogStorageRow log)
    {
        var service = log.ServiceName ?? "unknown";
        var severity = log.SeverityText ?? string.Empty;
        var body = log.Body ?? string.Empty;
        return $"{service}\u001f{severity}\u001f{body}";
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

        var expiredKeys = new List<string>();
        foreach (var pair in _buckets)
        {
            var elapsed = utcNow - pair.Value.LastSeenUtc;
            if (elapsed >= _window)
                expiredKeys.Add(pair.Key);
        }

        foreach (var key in expiredKeys)
        {
            var bucket = _buckets[key];
            EmitSummary(bucket, output);
            _buckets.Remove(key);
        }
    }

    private sealed class DedupBucket(LogStorageRow firstLog, DateTime firstSeenUtc)
    {
        public int Count { get; set; } = 1;
        public DateTime LastSeenUtc { get; set; } = firstSeenUtc;
        public LogStorageRow LastLog { get; set; } = firstLog;
    }
}

/// <summary>
///     Log emitted by deduplication.
///     <see cref="RepeatCount"/> is the number of suppressed duplicates for summary events.
/// </summary>
internal sealed record DeduplicatedLiveLog(
    LogStorageRow Log,
    int RepeatCount = 1,
    bool IsDuplicateSummary = false
);
