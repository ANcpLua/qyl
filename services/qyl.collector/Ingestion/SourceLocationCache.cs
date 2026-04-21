namespace Qyl.Collector.Ingestion;

[QylService(QylLifetime.Singleton)]
public sealed class SourceLocationCache
{
    public const int MaxCacheEntries = 10_000;
    public const int IdleTimeoutMinutes = 60;

    private readonly ExpiringCache<string, SourceLocation> _cache = new(
        MaxCacheEntries,
        TimeSpan.FromMinutes(IdleTimeoutMinutes),
        StringComparer.Ordinal);

    public SourceLocation? GetOrAdd(string key, Func<SourceLocation?> factory) =>
        _cache.GetOrAdd(key, factory);
}
