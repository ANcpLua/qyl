using Qyl.AdGuard.Companion.Telemetry;

namespace Qyl.AdGuard.Companion.Diagnostics;

internal sealed class NetworkBatchAnalyzer(CompanionTelemetry telemetry, CompanionStats stats)
{
    public NetworkBatchResult Analyze(NetworkBatchParams parameters)
    {
        var events = parameters.Events ?? [];
        using var activity = telemetry.StartActivity("adguard.network.batch");
        activity?.SetTag("qyl.adguard.network.events", events.Length);

        var blocked = events.Count(static item => IsBlockedByClient(item));
        activity?.SetTag("qyl.adguard.network.blocked_by_client", blocked);
        stats.RecordNetworkBatch(events.Length, blocked);

        var fullHostMap = events
            .Select(static item => new { Event = item, Host = TryHost(item.Url) })
            .Where(static item => item.Host is not null)
            .GroupBy(static item => item.Host!, StringComparer.OrdinalIgnoreCase)
            .Select(static group => new NetworkHostSummary(
                Host: group.Key,
                Total: group.Count(),
                BlockedByClient: group.Count(static item => IsBlockedByClient(item.Event)),
                LastError: group.LastOrDefault(static item => !string.IsNullOrWhiteSpace(item.Event.Error))?.Event.Error))
            .OrderByDescending(static item => item.BlockedByClient)
            .ThenByDescending(static item => item.Total)
            .ToArray();

        EmitPerHostDecisions(fullHostMap);

        return new NetworkBatchResult(
            Received: events.Length,
            BlockedByClient: blocked,
            Hosts: fullHostMap.Take(12).ToArray(),
            Message: telemetry.Enabled
                ? "Network evidence summarized and emitted as qyl companion telemetry."
                : "Network evidence summarized locally; set OTEL_EXPORTER_OTLP_ENDPOINT to emit qyl telemetry.");
    }

    private void EmitPerHostDecisions(NetworkHostSummary[] hostSummaries)
    {
        // One span per host per batch carries the qyl.tracker.* convention that the
        // collector's tracker-decision query and the qyl.mcp tools join on. Browser-layer
        // and dns-layer spans share this shape so source='browser'|'dns' is the only
        // distinguishing dimension at the query plane.
        foreach (var summary in hostSummaries)
        {
            using var hostActivity = telemetry.StartActivity("adguard.tracker.decision");
            if (hostActivity is null)
                continue;

            hostActivity.SetTag("qyl.tracker.source", "browser");
            hostActivity.SetTag("qyl.tracker.host", summary.Host);
            hostActivity.SetTag("qyl.tracker.total", summary.Total);
            hostActivity.SetTag("qyl.tracker.blocked", summary.BlockedByClient);
            hostActivity.SetTag("qyl.tracker.last_error", summary.LastError);
        }
    }

    private static bool IsBlockedByClient(NetworkEvent item) =>
        item.Error?.Contains("ERR_BLOCKED_BY_CLIENT", StringComparison.OrdinalIgnoreCase) is true ||
        item.Error?.Contains("blocked_by_client", StringComparison.OrdinalIgnoreCase) is true ||
        item.StatusCode is 0 && !string.IsNullOrWhiteSpace(item.Error);

    private static string? TryHost(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : null;
}

internal sealed class NetworkBatchParams
{
    public NetworkEvent[]? Events { get; init; }
}

internal sealed class NetworkEvent
{
    public string? Url { get; init; }

    public string? Type { get; init; }

    public int? StatusCode { get; init; }

    public string? Error { get; init; }

    public int? TabId { get; init; }
}

internal sealed record NetworkBatchResult(
    int Received,
    int BlockedByClient,
    NetworkHostSummary[] Hosts,
    string Message);

internal sealed record NetworkHostSummary(string Host, int Total, int BlockedByClient, string? LastError);
