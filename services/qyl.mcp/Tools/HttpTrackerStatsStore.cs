using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ANcpLua.Roslyn.Utilities.Web;
using Microsoft.Extensions.Logging;

namespace qyl.mcp.Tools;

/// <summary>
/// HTTP-backed <see cref="ITrackerStatsStore"/> that calls qyl.collector for
/// aggregated tracker-decision rows. Endpoint contract:
/// <list type="bullet">
///   <item><c>GET /api/v1/tracker-decisions/top?source={source}&amp;sinceHours={h}&amp;top={n}</c></item>
///   <item><c>GET /api/v1/tracker-decisions/by-site?host={host}&amp;sinceHours={h}</c></item>
/// </list>
/// When the collector endpoint is not yet wired, the store returns empty results
/// rather than throwing — the MCP tool then surfaces a hint to the user.
/// </summary>
public sealed partial class HttpTrackerStatsStore(
    HttpClient client,
    ILogger<HttpTrackerStatsStore> logger) : ITrackerStatsStore
{
    public async ValueTask<TopTrackersResult> GetTopTrackersAsync(string? source, int sinceHours, int top)
    {
        var clampedSince = Math.Clamp(sinceHours, 1, 24 * 30);
        var clampedTop = Math.Clamp(top, 1, 200);
        var url = QueryString.AppendPairs(
            $"/api/v1/tracker-decisions/top?sinceHours={clampedSince}&top={clampedTop}",
            ("source", source));

        try
        {
            var response = await client.GetFromJsonAsync(
                url,
                TrackerStatsHttpJsonContext.Default.TopTrackersResponse).ConfigureAwait(false);

            if (response is null)
                return Empty(source, clampedSince);

            return new TopTrackersResult(
                Source: source ?? "any",
                SinceHours: clampedSince,
                TotalEventsObserved: response.TotalEventsObserved,
                TotalBlocked: response.TotalBlocked,
                Items: response.Items?
                    .Select(static item => new TrackerHostCount(
                        item.Host,
                        item.Total,
                        item.Blocked,
                        item.LastReason))
                    .ToArray() ?? []);
        }
        catch (HttpRequestException ex)
        {
            LogTopTrackersFailed(ex);
            return Empty(source, clampedSince);
        }
    }

    public async ValueTask<SiteTrackerStatsResult> GetForSiteAsync(string siteHost, int sinceHours)
    {
        var clampedSince = Math.Clamp(sinceHours, 1, 24 * 30);
        var url = QueryString.AppendPairs(
            $"/api/v1/tracker-decisions/by-site?sinceHours={clampedSince}",
            ("host", siteHost));

        try
        {
            var response = await client.GetFromJsonAsync(
                url,
                TrackerStatsHttpJsonContext.Default.SiteTrackerResponse).ConfigureAwait(false);

            if (response is null)
                return EmptySite(siteHost, clampedSince);

            return new SiteTrackerStatsResult(
                SiteHost: siteHost,
                SinceHours: clampedSince,
                TotalEventsObserved: response.TotalEventsObserved,
                TotalBlocked: response.TotalBlocked,
                Decisions: response.Decisions?
                    .Select(static d => new SiteTrackerDecision(
                        d.Host,
                        d.Decision,
                        d.Reason,
                        d.OccurredAt))
                    .ToArray() ?? []);
        }
        catch (HttpRequestException ex)
        {
            LogSiteTrackerFailed(ex, siteHost);
            return EmptySite(siteHost, clampedSince);
        }
    }

    private static TopTrackersResult Empty(string? source, int sinceHours) =>
        new(source ?? "any", sinceHours, 0, 0, []);

    private static SiteTrackerStatsResult EmptySite(string siteHost, int sinceHours) =>
        new(siteHost, sinceHours, 0, 0, []);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Failed to fetch top trackers from collector — endpoint may not be wired yet.")]
    private partial void LogTopTrackersFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Failed to fetch tracker stats for site {SiteHost}.")]
    private partial void LogSiteTrackerFailed(Exception ex, string siteHost);
}

#region HTTP Response DTOs

internal sealed record TopTrackersResponse(
    [property: JsonPropertyName("total_events_observed")]
    long TotalEventsObserved,
    [property: JsonPropertyName("total_blocked")]
    long TotalBlocked,
    [property: JsonPropertyName("items")] List<TrackerHostCountResponse>? Items);

internal sealed record TrackerHostCountResponse(
    [property: JsonPropertyName("host")] string Host,
    [property: JsonPropertyName("total")] long Total,
    [property: JsonPropertyName("blocked")] long Blocked,
    [property: JsonPropertyName("last_reason")]
    string? LastReason);

internal sealed record SiteTrackerResponse(
    [property: JsonPropertyName("total_events_observed")]
    long TotalEventsObserved,
    [property: JsonPropertyName("total_blocked")]
    long TotalBlocked,
    [property: JsonPropertyName("decisions")]
    List<SiteTrackerDecisionResponse>? Decisions);

internal sealed record SiteTrackerDecisionResponse(
    [property: JsonPropertyName("host")] string Host,
    [property: JsonPropertyName("decision")] string Decision,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("occurred_at")]
    DateTime OccurredAt);

#endregion

[JsonSerializable(typeof(TopTrackersResponse))]
[JsonSerializable(typeof(SiteTrackerResponse))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal sealed partial class TrackerStatsHttpJsonContext : JsonSerializerContext;
