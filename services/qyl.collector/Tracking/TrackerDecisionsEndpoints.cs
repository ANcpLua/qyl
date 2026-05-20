using Qyl.Collector.Storage;

namespace Qyl.Collector.Tracking;

public sealed record TopTrackerResponse(
    long TotalEventsObserved,
    long TotalBlocked,
    IReadOnlyList<TopTrackerItem> Items);

public sealed record TopTrackerItem(string Host, long Total, long Blocked, string? LastReason);

public sealed record SiteTrackerResponse(
    long TotalEventsObserved,
    long TotalBlocked,
    IReadOnlyList<SiteTrackerDecisionRow> Decisions);

public sealed record SiteTrackerDecisionRow(
    string Host,
    string Decision,
    string? Reason,
    DateTimeOffset OccurredAt);

internal static class TrackerDecisionsEndpoints
{
    [QylMapEndpoints]
    public static void MapTrackerDecisionsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/tracker-decisions/top", static async Task<IResult> (
            DuckDbStore store,
            TimeProvider timeProvider,
            CancellationToken ct,
            string? source = null,
            int sinceHours = 168,
            int top = 20) =>
        {
            var clampedSince = Math.Clamp(sinceHours, 1, 24 * 30);
            var sinceNanos = ToUnixNanos(timeProvider.GetUtcNow().AddHours(-clampedSince));

            var totals = await store.GetTrackerDecisionTotalsAsync(source, sinceNanos, ct).ConfigureAwait(false);
            var rows = await store.GetTopTrackerDecisionsAsync(source, sinceNanos, top, ct).ConfigureAwait(false);

            var items = rows.Select(static row => new TopTrackerItem(
                Host: row.Host,
                Total: row.Total,
                Blocked: row.Blocked,
                LastReason: row.LastReason)).ToArray();

            return TypedResults.Ok(new TopTrackerResponse(
                TotalEventsObserved: totals.TotalEvents,
                TotalBlocked: totals.TotalBlocked,
                Items: items));
        });

        app.MapGet("/api/v1/tracker-decisions/by-site", static async Task<IResult> (
            DuckDbStore store,
            TimeProvider timeProvider,
            CancellationToken ct,
            string host,
            int sinceHours = 24,
            int limit = 100) =>
        {
            if (string.IsNullOrWhiteSpace(host))
                return TypedResults.BadRequest("host query parameter is required.");

            var clampedSince = Math.Clamp(sinceHours, 1, 24 * 30);
            var sinceNanos = ToUnixNanos(timeProvider.GetUtcNow().AddHours(-clampedSince));

            var totals = await store.GetTrackerDecisionTotalsAsync(source: null, sinceNanos, ct).ConfigureAwait(false);
            var decisions = await store.GetTrackerDecisionsForHostAsync(host, sinceNanos, limit, ct)
                .ConfigureAwait(false);

            var rows = decisions.Select(static row => new SiteTrackerDecisionRow(
                Host: row.Host,
                Decision: row.Decision,
                Reason: row.Reason,
                OccurredAt: FromUnixNanos(row.OccurredAtUnixNanos))).ToArray();

            return TypedResults.Ok(new SiteTrackerResponse(
                TotalEventsObserved: totals.TotalEvents,
                TotalBlocked: totals.TotalBlocked,
                Decisions: rows));
        });
    }

    private static long ToUnixNanos(DateTimeOffset timestamp) =>
        timestamp.ToUnixTimeMilliseconds() * 1_000_000L;

    private static DateTimeOffset FromUnixNanos(ulong nanos) =>
        DateTimeOffset.FromUnixTimeMilliseconds((long)(nanos / 1_000_000UL));
}
