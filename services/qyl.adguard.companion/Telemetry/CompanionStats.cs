namespace Qyl.AdGuard.Companion.Telemetry;

/// <summary>
/// Thread-safe in-memory counters for the native messaging host.
/// Snapshotted by the <c>get_stats</c> handler so the popup UI can render a mini-dashboard
/// without going through the OTLP exporter.
/// </summary>
internal sealed class CompanionStats(TimeProvider timeProvider)
{
    private readonly DateTimeOffset _startedAt = timeProvider.GetUtcNow();
    private long _requestsTotal;
    private long _requestsFailed;
    private long _networkBatches;
    private long _networkEvents;
    private long _blockedByClient;
    private long _lastActivityTicks;

    public void RecordRequest(bool success)
    {
        Interlocked.Increment(ref _requestsTotal);
        if (!success)
            Interlocked.Increment(ref _requestsFailed);
        Interlocked.Exchange(ref _lastActivityTicks, timeProvider.GetUtcNow().UtcTicks);
    }

    public void RecordNetworkBatch(int events, int blocked)
    {
        Interlocked.Increment(ref _networkBatches);
        Interlocked.Add(ref _networkEvents, events);
        Interlocked.Add(ref _blockedByClient, blocked);
    }

    public StatsSnapshot Snapshot()
    {
        var now = timeProvider.GetUtcNow();
        var lastTicks = Interlocked.Read(ref _lastActivityTicks);
        DateTimeOffset? lastActivity = lastTicks is 0
            ? null
            : new DateTimeOffset(lastTicks, TimeSpan.Zero);

        return new StatsSnapshot(
            SchemaVersion: 1,
            StartedAtUtc: _startedAt,
            NowUtc: now,
            UptimeSeconds: (long)(now - _startedAt).TotalSeconds,
            RequestsTotal: Interlocked.Read(ref _requestsTotal),
            RequestsFailed: Interlocked.Read(ref _requestsFailed),
            NetworkBatches: Interlocked.Read(ref _networkBatches),
            NetworkEvents: Interlocked.Read(ref _networkEvents),
            BlockedByClient: Interlocked.Read(ref _blockedByClient),
            LastActivityUtc: lastActivity);
    }
}

internal sealed record StatsSnapshot(
    int SchemaVersion,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset NowUtc,
    long UptimeSeconds,
    long RequestsTotal,
    long RequestsFailed,
    long NetworkBatches,
    long NetworkEvents,
    long BlockedByClient,
    DateTimeOffset? LastActivityUtc);
