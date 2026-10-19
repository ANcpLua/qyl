using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Qyl.Api.Contracts.Common.Pagination;
using Qyl.Collector.Analysis;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Hosting;

internal static partial class CollectorEndpointExtensions
{
    private static async Task<IResult> GetMetricPointsAsync(HttpContext context,
        [FromRoute(Name = "metric_name")] string metricName,
        [FromRoute(Name = "series_id")] string seriesId, IQylStore store, CancellationToken ct)
    {
        if (ContractQueryParser.ParseMetricPoints(context.Request, out var query) is { } error) return error;
        var project = ResolveProjectScope(context);
        if ((await store.ListMetricSeriesAsync(project, metricName, [], 1, seriesId: seriesId, ct: ct)).SingleOrDefault() is not { } series) return ContractErrorResults.NotFound("metric series", seriesId);
        try { return Results.Ok(await ReadMetricPointPageAsync(store, series, project, query, ct)); }
        catch (AnalysisInputException invalid)
        { return ContractErrorResults.Validation(invalid.Field, invalid.Description, invalid.Code); }
    }

    private static async Task<CursorPageMetricPoint> ReadMetricPointPageAsync(IQylStore store, MetricSeriesRow series,
        string project, ParsedMetricPointParameters query, CancellationToken ct)
    {
        if (!ContractLimits.TryResolve(query.Limit, ContractLimits.DefaultPageLimit, ContractLimits.MaxPageLimit, out var limit))
            throw new AnalysisInputException("limit", $"Limit must be between 1 and {ContractLimits.MaxPageLimit}.", "limit.out_of_range");
        if (query.Start < DateTimeOffset.UnixEpoch || query.End < DateTimeOffset.UnixEpoch)
            throw new AnalysisInputException("start_time", "Metric times must be on or after the Unix epoch.", "range.invalid");
        if (query.Start is { } start && query.End is { } end && start > end)
            throw new AnalysisInputException("end_time", "The range end must not precede its start.", "range.inverted");
        MetricPointCursor? cursor = null;
        if (query.Cursor is { } encoded)
        {
            // Bind continuations to their exact project, instrument and stream; they cannot silently page another series.
            if (!PageCursorCodec.TryDecode(encoded, PageCursorKind.MetricPoints, 5, out var fields) ||
                fields[0] != project || fields[1] != series.MetricName || fields[2] != series.SeriesId ||
                !PageCursorCodec.TryParseHex(fields[3], out var time))
                throw new AnalysisInputException("cursor", "The metric point cursor is invalid for this series.", "cursor.invalid");
            cursor = new MetricPointCursor(time, fields[4]);
        }
        var observations = await store.ListMetricPointsAsync(new MetricPointQuery
        {
            ProjectId = project, MetricName = series.MetricName, SeriesId = series.SeriesId, Limit = limit + 1,
            StartUnixNano = query.Start is { } from ? QylTimeConversions.ToUnixNanoUnsigned(from) : null,
            EndUnixNano = query.End is { } until ? QylTimeConversions.ToUnixNanoUnsigned(until) : null, Cursor = cursor
        }, ct);
        var more = observations.Count > limit;
        var page = observations.Take(limit).ToArray();
        return new CursorPageMetricPoint
        {
            Items = page.Select(item => MetricMapper.ToContract(item, series)).ToArray(), HasMore = more,
            NextCursor = more ? PageCursorCodec.Encode(PageCursorKind.MetricPoints, project, series.MetricName,
                series.SeriesId, PageCursorCodec.Hex(page[^1].Observation.TimeUnixNano), page[^1].Observation.ObservationId) : null
        };
    }

    private static async Task<JsonObject> SelectedMetricSeriesAsync(JsonObject input, IQylStore store, string project,
        AnalysisHandles handles, DateTimeOffset now, CancellationToken ct)
    {
        if (J.String(input, "cursor") is not null)
            throw new AnalysisInputException("cursor", "Use points_cursor when selecting one series.", "cursor.invalid");
        AnalysisMatchers.Validate(input["matchers"]);
        var window = AnalysisWindow.Resolve(input, now);
        string? pointCursor = null;
        if (J.String(input, "points_cursor") is { } continuation)
        {
            var saved = handles.Read(continuation, project, "metric-points");
            if (J.String(saved, "metric_name") != J.String(input, "metric_name") || J.String(saved, "series_id") != J.String(input, "series_id"))
                throw new AnalysisInputException("points_cursor", "The point cursor belongs to another series.", "cursor.invalid");
            window = AnalysisWindow.Resolve(saved["window"]!.AsObject(), now);
            pointCursor = J.String(saved, "cursor");
        }
        var name = J.String(input, "metric_name")!;
        var id = J.String(input, "series_id")!;
        var series = (await store.ListMetricSeriesAsync(project, name, [], 1, seriesId: id, ct: ct)).SingleOrDefault();
        if (series is not null && !AnalysisMatchers.Match(AnalysisEngine.SeriesRecord(series), input["matchers"])) series = null;
        var page = series is null ? new CursorPageMetricPoint { Items = [], HasMore = false }
            : await ReadMetricPointPageAsync(store, series, project,
                new ParsedMetricPointParameters(window.Start, window.End,
                    input["points_limit"]?.GetValue<int>(), pointCursor), ct);
        if (page.NextCursor is { } next)
            page = new CursorPageMetricPoint
            {
                Items = page.Items, HasMore = page.HasMore,
                NextCursor = handles.Issue(project, "metric-points", J.O(("window", window.Json),
                    ("metric_name", name), ("series_id", id), ("cursor", next)))
            };
        var items = new JsonArray();
        if (series is not null) items.Add(JsonSerializer.SerializeToNode(MetricMapper.ToContract(series), QylSerializerContext.Default.MetricSeries));
        return J.O(("items", items), ("has_more", false),
            ("points", JsonSerializer.SerializeToNode(page, QylSerializerContext.Default.CursorPageMetricPoint)),
            ("window", window.Json), ("coverage", J.O(("examined", page.Items.Count), ("matched", page.HasMore ? null : (int?)page.Items.Count),
                ("returned", page.Items.Count), ("truncated", page.HasMore), ("scan_complete", !page.HasMore), ("as_of", window.End.ToString("O")))));
    }
}
