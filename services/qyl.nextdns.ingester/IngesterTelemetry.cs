using System.Diagnostics;

namespace Qyl.NextDns.Ingester;

/// <summary>
/// Each NextDNS decision becomes a short-lived span on this source so that the
/// existing OTLP path into qyl.collector picks it up. Span tags follow the
/// shared <c>qyl.tracker.*</c> convention so the MCP <c>qyl.tracker_stats_*</c>
/// tools can query both browser-layer and dns-layer decisions in one shape.
/// </summary>
internal static class IngesterTelemetry
{
    public const string SourceName = "qyl.nextdns.ingester";

    private static readonly ActivitySource s_source = new(SourceName);

    public static void RecordDecision(NextDnsLogEntry entry)
    {
        using var activity = s_source.StartActivity("nextdns.decision", ActivityKind.Internal);
        if (activity is null)
            return;

        var host = entry.Domain ?? entry.Root ?? "unknown";
        var blocked = string.Equals(entry.Status, "blocked", StringComparison.OrdinalIgnoreCase);
        var reason = entry.Reasons?.FirstOrDefault()?.Name;

        activity.SetTag("qyl.tracker.source", "dns");
        activity.SetTag("qyl.tracker.host", host);
        activity.SetTag("qyl.tracker.root", entry.Root);
        activity.SetTag("qyl.tracker.blocked", blocked);
        activity.SetTag("qyl.tracker.status", entry.Status);
        activity.SetTag("qyl.tracker.reason", reason);
        activity.SetTag("qyl.tracker.protocol", entry.Protocol);
        activity.SetTag("qyl.tracker.entry_id", entry.Id);

        if (entry.Timestamp is { } ts)
            activity.SetTag("qyl.tracker.observed_at", ts.UtcDateTime.ToString("O"));
    }
}
