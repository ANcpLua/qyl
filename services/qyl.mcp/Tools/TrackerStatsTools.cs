using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ANcpLua.Roslyn.Utilities.Web;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

/// <summary>
/// MCP tools that surface tracker-block evidence captured by the qyl AdGuard Companion
/// (browser-layer) and the qyl NextDNS ingester (DNS-layer). Both layers write into the
/// shared <c>tracker_decisions</c> table on qyl.collector with a <c>source</c> dimension.
/// </summary>
[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
internal sealed partial class TrackerStatsTools(ITrackerStatsStore store)
{
    [QylCapability("network_observability", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "qyl.tracker_stats_top", Title = "Top Trackers",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true,
        TaskSupport = ToolTaskSupport.Optional)]
    public partial Task<string> TopTrackersAsync(
        string? source = null,
        int sinceHours = 168,
        int top = 20) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var result = await store.GetTopTrackersAsync(source, sinceHours, top)
                .ConfigureAwait(false);
            return result.Items.Length is 0
                ? $"No tracker decisions found for source='{source ?? "any"}' in the last {sinceHours}h. " +
                  "If the qyl-adguard-companion is running but no spans land in DuckDB, " +
                  "verify OTEL_EXPORTER_OTLP_ENDPOINT in its environment and re-run."
                : JsonSerializer.Serialize(result, TrackerStatsJsonContext.Default.TopTrackersResult);
        });

    [QylCapability("network_observability", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "qyl.tracker_stats_for_site", Title = "Tracker Stats For Site",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true,
        TaskSupport = ToolTaskSupport.Optional)]
    public partial Task<string> TrackerStatsForSiteAsync(
        string siteHost,
        int sinceHours = 24) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(siteHost))
                return "siteHost is required (e.g. 'news.ycombinator.com').";

            var result = await store.GetForSiteAsync(siteHost, sinceHours)
                .ConfigureAwait(false);
            return result.Decisions.Length is 0
                ? $"No tracker decisions recorded for site '{siteHost}' in the last {sinceHours}h."
                : JsonSerializer.Serialize(result, TrackerStatsJsonContext.Default.SiteTrackerStatsResult);
        });
}

#region Data Models

public record TrackerHostCount(string Host, long Total, long Blocked, string? LastReason);

public record TopTrackersResult(
    string Source,
    int SinceHours,
    long TotalEventsObserved,
    long TotalBlocked,
    TrackerHostCount[] Items);

public record SiteTrackerDecision(
    string Host,
    string Decision,
    string? Reason,
    DateTime OccurredAt);

public record SiteTrackerStatsResult(
    string SiteHost,
    int SinceHours,
    long TotalEventsObserved,
    long TotalBlocked,
    SiteTrackerDecision[] Decisions);

#endregion

#region Store Interface

public interface ITrackerStatsStore
{
    ValueTask<TopTrackersResult> GetTopTrackersAsync(string? source, int sinceHours, int top);

    ValueTask<SiteTrackerStatsResult> GetForSiteAsync(string siteHost, int sinceHours);
}

#endregion

[JsonSerializable(typeof(TopTrackersResult))]
[JsonSerializable(typeof(SiteTrackerStatsResult))]
[JsonSerializable(typeof(TrackerHostCount[]))]
[JsonSerializable(typeof(SiteTrackerDecision[]))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal sealed partial class TrackerStatsJsonContext : JsonSerializerContext;
