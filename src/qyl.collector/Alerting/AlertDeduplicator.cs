namespace qyl.collector.Alerting;

/// <summary>
///     Suppresses duplicate alerts within a configurable time window.
///     Key: rule_id + fingerprint. Only fires on first occurrence, then suppresses until window expires.
/// </summary>
public sealed partial class AlertDeduplicator
{
    private readonly ConcurrentDictionary<string, DeduplicationEntry> _entries = new();
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AlertDeduplicator> _logger;
    private readonly TimeSpan _defaultWindow;

    public AlertDeduplicator(
        ILogger<AlertDeduplicator> logger,
        TimeProvider? timeProvider = null,
        TimeSpan? defaultWindow = null)
    {
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _defaultWindow = defaultWindow ?? TimeSpan.FromMinutes(5);
    }

    /// <summary>
    ///     Returns true if the alert should be sent (first occurrence or window expired).
    ///     Returns false if suppressed as duplicate.
    /// </summary>
    public bool ShouldFire(AlertEvent alertEvent)
    {
        // Resolved alerts always pass through
        if (alertEvent.Status != "firing")
            return true;

        var key = BuildKey(alertEvent.RuleName, alertEvent.Condition, alertEvent.QueryResult);
        var now = _timeProvider.GetUtcNow();

        var entry = _entries.AddOrUpdate(
            key,
            _ => new DeduplicationEntry
            {
                FirstSeen = now,
                LastSeen = now,
                Count = 1
            },
            (_, existing) =>
            {
                existing.LastSeen = now;
                existing.Count++;
                return existing;
            });

        if (entry.Count == 1)
        {
            LogFirstOccurrence(_logger, alertEvent.RuleName, key);
            return true;
        }

        // Check if window has expired since first seen
        if (now - entry.FirstSeen >= _defaultWindow)
        {
            // Reset the window
            entry.FirstSeen = now;
            entry.Count = 1;
            LogWindowExpired(_logger, alertEvent.RuleName, key);
            return true;
        }

        LogSuppressed(_logger, alertEvent.RuleName, entry.Count, key);
        return false;
    }

    /// <summary>
    ///     Removes expired entries to prevent unbounded memory growth.
    /// </summary>
    public int PurgeExpired()
    {
        var now = _timeProvider.GetUtcNow();
        var purged = 0;

        foreach (var kvp in _entries)
        {
            if (now - kvp.Value.LastSeen > _defaultWindow * 2)
            {
                if (_entries.TryRemove(kvp.Key, out _))
                    purged++;
            }
        }

        return purged;
    }

    /// <summary>
    ///     Gets the current deduplication entries for diagnostics.
    /// </summary>
    public IReadOnlyDictionary<string, DeduplicationEntry> Entries => _entries;

    private static string BuildKey(string ruleName, string condition, double queryResult) =>
        string.Create(CultureInfo.InvariantCulture, $"{ruleName}:{condition}:{queryResult:F2}");

    // ==========================================================================
    // Log Messages
    // ==========================================================================

    [LoggerMessage(Level = LogLevel.Debug, Message = "Alert '{RuleName}' first occurrence (key={Key})")]
    private static partial void LogFirstOccurrence(ILogger logger, string ruleName, string key);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Alert '{RuleName}' suppressed (count={Count}, key={Key})")]
    private static partial void LogSuppressed(ILogger logger, string ruleName, int count, string key);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Alert '{RuleName}' dedup window expired, re-firing (key={Key})")]
    private static partial void LogWindowExpired(ILogger logger, string ruleName, string key);
}

/// <summary>
///     Tracks deduplication state for a single alert fingerprint.
/// </summary>
public sealed class DeduplicationEntry
{
    public DateTimeOffset FirstSeen { get; set; }
    public DateTimeOffset LastSeen { get; set; }
    public int Count { get; set; }
}
