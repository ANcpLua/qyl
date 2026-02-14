namespace qyl.collector.Ingestion;

public sealed class SourceLocationCache
{
    public const int MaxCacheEntries = 10_000;
    public const int IdleTimeoutMinutes = 60;

    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<string> _insertionOrder = new();
    private readonly TimeSpan _idleTimeout = TimeSpan.FromMinutes(IdleTimeoutMinutes);

    public SourceLocation? GetOrAdd(string key, Func<SourceLocation?> factory)
    {
        var now = TimeProvider.System.GetUtcNow();

        if (_cache.TryGetValue(key, out var existing))
        {
            existing.Touch(now);
            return existing.Location;
        }

        var created = factory();
        var entry = new CacheEntry(created, now);

        if (_cache.TryAdd(key, entry))
        {
            _insertionOrder.Enqueue(key);
            EvictIfNeeded(now);
            return created;
        }

        if (_cache.TryGetValue(key, out existing))
        {
            existing.Touch(now);
            return existing.Location;
        }

        return created;
    }

    private void EvictIfNeeded(DateTimeOffset now)
    {
        while (_cache.Count > MaxCacheEntries && _insertionOrder.TryDequeue(out var oldest))
        {
            _cache.TryRemove(oldest, out _);
        }

        foreach (var (key, value) in _cache)
        {
            if (now - value.LastAccessUtc <= _idleTimeout)
                continue;

            _cache.TryRemove(key, out _);
        }
    }

    private sealed class CacheEntry
    {
        public CacheEntry(SourceLocation? location, DateTimeOffset lastAccessUtc)
        {
            Location = location;
            LastAccessUtc = lastAccessUtc;
        }

        public SourceLocation? Location { get; }
        public DateTimeOffset LastAccessUtc { get; private set; }

        public void Touch(DateTimeOffset now) => LastAccessUtc = now;
    }
}
