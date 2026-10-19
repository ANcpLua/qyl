using System.Text.Json.Nodes;
using Qyl.Api.Contracts.Mcp.Tools;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Analysis;

internal sealed partial class AnalysisEngine
{
    private static string MetricKind(byte kind) => kind switch { 1 => "gauge", 2 => "sum", 3 => "histogram", 4 => "exponential_histogram", 5 => "summary", _ => throw new InvalidDataException("Unknown persisted metric kind.") };
    internal static AnalysisRecord SeriesRecord(MetricSeriesRow s) => new(s.SeriesId, null, null, null, s.ServiceName ?? "unknown_service", s.MetricName, s.LastSeenUnixNano, s.LastSeenUnixNano, false, 0, J.Attributes(s.AttributesJson), J.Attributes(s.ResourceJson));
    private List<MetricSeriesRow> SeriesRows(JsonObject input) =>
    [
        .. snapshot.Series.Where(s =>
            (J.String(input, "metric_name") is not { } name || s.MetricName == name)
            && (J.String(input, "series_id") is not { } seriesId || s.SeriesId == seriesId)
            && (J.String(input, "name_prefix") is not { } prefix ||
                s.MetricName.StartsWith(prefix, StringComparison.Ordinal))
            && (J.String(input, "service_name") is not { } service || s.ServiceName == service)
            && (J.String(input, "text") is not { } text ||
                s.MetricName.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                s.Description?.Contains(text, StringComparison.OrdinalIgnoreCase) == true)
            && (J.String(input, "kind") is not { } kind || MetricKind(s.MetricKind) == kind)
            && (J.String(input, "unit") is not { } unit || s.Unit == unit)
            && AnalysisMatchers.Match(SeriesRecord(s), input["matchers"]))
    ];
    private JsonObject Metrics(JsonObject input)
    {
        var items = SeriesRows(input).GroupBy(static row => row.InstrumentId ?? MetricStorageMapper.InstrumentId(row))
            .Select(static group => MetricStorageMapper.ToCatalogEntry(group.ToArray()))
            .OrderBy(static entry => entry.MetricName, StringComparer.Ordinal)
            .ThenBy(static entry => entry.InstrumentId, StringComparer.Ordinal)
            .Select(static entry => JsonSerializer.SerializeToNode(MetricMapper.ToContract(entry), QylSerializerContext.Default.MetricDescriptor)!.AsObject());
        return Page(input, "items", items);
    }
    private JsonObject MetricSeries(JsonObject input)
    {
        var rows = SeriesRows(input).Where(s => PointsBySeries[s.SeriesId].Any(p => _window.Contains(p.TimeUnixNano))).OrderBy(s => s.SeriesId, StringComparer.Ordinal).ToList();
        var result = Page(input, "items", rows.Select(s =>
        { var item = JsonSerializer.SerializeToNode(MetricMapper.ToContract(s), QylSerializerContext.Default.MetricSeries)!.AsObject(); item["new"] = _window.Contains(s.FirstSeenUnixNano); item["stale"] = s.LastSeenUnixNano < _window.StartNs; return item; }));
        var keys = rows.SelectMany(s => SeriesRecord(s).Attributes.Select(p => (p.Key, p.Value))).GroupBy(p => p.Key).Select(g =>
        { var values = g.GroupBy(p => p.Value?.ToJsonString() ?? "null").ToList(); return J.O(("key", g.Key), ("cardinality", values.Count), ("cardinality_exact", snapshot.ScanComplete), ("top_values", J.A(values.OrderByDescending(v => v.Count()).Take(10).Select(v => J.O(("value", ContractValue(v.First().Value)), ("count", v.Count()), ("share", v.Count() / (double)Math.Max(1, rows.Count)))))), ("coverage_share", g.Count() / (double)Math.Max(1, rows.Count))); }).ToList();
        result["keys"] = J.A(keys); result["suggested_group_by"] = J.N(keys.Where(k => J.Number(k, "cardinality") > 1 && J.Number(k, "cardinality") <= 20).Select(k => J.String(k, "key")!)); return result;
    }
    private static JsonArray ContractAttributes(JsonObject source)
    {
        return J.A(source.Select(static p => J.O(("key", p.Key), ("value", ContractValue(p.Value)))));
    }
    private static JsonNode? ContractValue(JsonNode? value)
    {
        if (value is JsonArray array) return J.A(array.Select(ContractValue));
        if (value?.GetValueKind() != JsonValueKind.Number) return value?.DeepClone();
        var text = value.ToJsonString();
        return long.TryParse(text, CultureInfo.InvariantCulture, out var integer)
            ? J.O(("type", "int"), ("value", integer.ToString(CultureInfo.InvariantCulture)))
            : J.O(("type", "double"), ("value", value));
    }
    private JsonObject QueryMetric(JsonObject input)
    {
        var rows = SeriesRows(input); var name = J.String(input, "metric_name") ?? throw new AnalysisInputException("metric_name", "metric_name is required.");
        if (rows.Count is 0) throw new AnalysisInputException("metric_name", "Metric was not found in this project.", "metric.not_found");
        var aggregation = J.String(input, "aggregation") ?? "avg"; var function = J.String(input, "function") ?? "none";
        if (rows.Select(static row => (row.MetricKind, row.Unit, row.Temporality, row.IsMonotonic)).Distinct().Skip(1).Any())
            throw new MetricQueryConflictException();
        if (rows.SelectMany(row => PointsBySeries[row.SeriesId]).Any(point => _window.Contains(point.TimeUnixNano) && point.HasConflict != 0))
            throw new MetricQueryConflictException("Different measurements exist at one series timestamp; inspect the original observations before aggregating this window.");
        var supported = MetricMapper.SupportedAggregations(rows.Select(static row => row.MetricKind))
            .Select(static aggregation => aggregation.ToString().ToLowerInvariant()).ToArray();
        if (!supported.Contains(aggregation, StringComparer.Ordinal))
            throw new AnalysisInputException("aggregation", $"'{aggregation}' does not apply to this metric; use one of: {string.Join(", ", supported)}.",
                aggregation.StartsWith('p') ? "aggregation.requires_histogram" : "aggregation.unsupported");
        if (aggregation.StartsWith('p') && rows.SelectMany(row => PointsBySeries[row.SeriesId])
                .FirstOrDefault(point => _window.Contains(point.TimeUnixNano) && point.ProjectionUnavailableReason is not null) is { } unavailable)
            throw new AnalysisInputException("aggregation", unavailable.ProjectionUnavailableReason!, "metric.projection_unavailable");
        // A histogram's points are already the changes of their intervals; a change or a rate applies to their count or sum.
        var histogram = rows[0].MetricKind >= 3;
        if (histogram && function != "none" && aggregation is not ("count" or "sum"))
            throw new AnalysisInputException("function", $"function={function} applies to a histogram's count or sum; use aggregation count or sum, or function none.", "function.unsupported");
        var groupBy = (input["group_by"] as JsonArray)?.Select(J.Text).ToArray() ?? [];
        var step = (long)J.Number(input, "step_ms", QueryMetricInput.StepMsDefault);
        if (step is < QueryMetricInput.StepMsMinimum or > QueryMetricInput.StepMsMaximum) throw new AnalysisInputException("step_ms", $"Use a step_ms from {QueryMetricInput.StepMsMinimum} to {QueryMetricInput.StepMsMaximum}.", "step_ms.out_of_range");
        var streams = rows.GroupBy(s => string.Join('\0', groupBy.Select(key => J.Text(SeriesRecord(s).Value(key))))).ToList();
        var output = new List<JsonObject>();
        foreach (var stream in streams)
        {
            var values = new List<(ulong Time, double? Value, MetricPointRow Point)>();
            foreach (var series in stream)
            {
                var points = PointsBySeries[series.SeriesId].OrderBy(p => p.TimeUnixNano).ToList();
                // The last point that recorded a measurement; a NO_RECORDED_VALUE point carries none.
                MetricPointRow? previous = null;
                foreach (var point in points)
                {
                    ct.ThrowIfCancellationRequested();
                    var value = function == "none" ? point.Value : Change(series, point, previous, function);
                    var recorded = series is { MetricKind: >= 3, Temporality: 2 }
                        ? MetricHistogram.HistogramDelta(point, previous, _window.StartNs) : point;
                    if (_window.Contains(point.TimeUnixNano) && recorded is not null) values.Add((point.TimeUnixNano, value, recorded));
                    if (point.Value.HasValue || point.HistogramCount.HasValue) previous = point;
                }
            }
            var byBucket = values.GroupBy(p => (long)((p.Time - _window.StartNs) / ((ulong)step * 1_000_000))).ToDictionary(g => g.Key, g => g.ToList());
            var buckets = new JsonArray();
            var bucketCount = (int)Math.Ceiling((_window.End - _window.Start).TotalMilliseconds / step);
            if (bucketCount > 10000) throw new AnalysisInputException("step_ms", "Increase step_ms so the window contains no more than 10000 buckets.");
            for (var i = 0; i < bucketCount; i++)
            {
                ct.ThrowIfCancellationRequested();
                var start = _window.StartNs + (ulong)i * (ulong)step * 1_000_000;
                var bucket = byBucket.GetValueOrDefault(i) ?? [];
                double? value;
                if (histogram) value = function == "rate" ? HistogramRate(bucket.Select(static p => p.Point), aggregation) : MetricHistogram.Histogram([.. bucket.Select(p => p.Point)], aggregation);
                else
                {
                    var recorded = bucket.Where(p => p.Value.HasValue).ToList(); var numbers = recorded.Select(p => p.Value!.Value).ToList();
                    value = numbers.Count is 0 ? null : function == "increase" ? numbers.Sum() : aggregation switch
                    {
                        "min" => numbers.Min(), "max" => numbers.Max(), "sum" => numbers.Sum(), "count" => numbers.Count,
                        "last" => MetricHistogram.Latest(recorded.Select(p => (p.Time, p.Value))), _ => numbers.Average()
                    };
                }
                buckets.Add((JsonNode)J.O(("bucket_start", J.Time(start)), ("value", value), ("point_count", bucket.Count)));
            }
            var attributes = new JsonObject(); foreach (var key in groupBy) attributes[key] = SeriesRecord(stream.First()).Value(key)?.DeepClone() ?? JsonValue.Create("");
            output.Add(J.O(("attributes", ContractAttributes(attributes)), ("buckets", buckets)));
        }
        var limit = Bounded(input, "series_limit", QueryMetricInput.SeriesLimitDefault, QueryMetricInput.SeriesLimitMinimum, QueryMetricInput.SeriesLimitMaximum);
        // A count is of points or observations, not of the metric's unit; a rate is per second.
        var unit = aggregation == "count" ? "1" : rows[0].Unit;
        if (function == "rate" && (histogram || aggregation != "count")) unit += "/s";
        var result = J.O(("name", name), ("kind", MetricKind(rows[0].MetricKind)), ("unit", unit), ("aggregation", aggregation), ("step_ms", step), ("start_time", _window.Start.ToString("O")), ("end_time", _window.End.ToString("O")), ("series", J.A(output.Take(limit))), ("truncated", output.Count > limit),
            ("method", histogram ? function == "rate" ? "per-series histogram deltas over the intervals they recorded, summed across series; an unknown interval leaves its bucket unknown" : "per-series histogram deltas merged, linear interpolation; unknown intervals omitted" : function == "none" ? "recorded scalar points aggregated" : "per-series counter differences across resets"), ("numeric_representation", "float64"), ("approximate", histogram && aggregation.StartsWith('p')), ("exemplar_trace_ids", snapshot.Exemplars.Where(e => _window.Contains(e.TimeUnixNano) && rows.Any(r => r.SeriesId == e.SeriesId)).Select(e => e.TraceId).Distinct(StringComparer.Ordinal).Take(3)), ("coverage", Coverage(snapshot.Points.Count, rows.Sum(s => PointsBySeries[s.SeriesId].Count(p => _window.Contains(p.TimeUnixNano))), Math.Min(output.Count, limit), output.Count > limit)));
        if (input["compare_to"] is { } compare)
        {
            var copy = input.DeepClone().AsObject(); copy.Remove("compare_to"); copy.Remove("ratio_of"); var current = _window; var offset = compare is JsonObject ? J.Number(compare, "offset_ms") : (_window.End - _window.Start).TotalMilliseconds;
            _window = new(_window.Start.AddMilliseconds(-offset), _window.End.AddMilliseconds(-offset)); var comparison = QueryMetric(copy); result["comparison"] = J.O(("window", _window.Json), ("series", comparison["series"])); _window = current;
        }
        if (input["ratio_of"] is JsonObject ratio)
        {
            // The denominator is another metric: the numerator's exact series selects nothing in it.
            var copy = input.DeepClone().AsObject(); copy.Remove("ratio_of"); copy.Remove("series_id"); copy["metric_name"] = ratio["metric_name"]?.DeepClone(); copy["aggregation"] = ratio["aggregation"]?.DeepClone() ?? JsonValue.Create(aggregation); copy["matchers"] = ratio["matchers"]?.DeepClone();
            copy["series_limit"] = QueryMetricInput.SeriesLimitMaximum;
            var denominator = QueryMetric(copy);
            DivideMetricSeries(result["series"], denominator["series"]);
            DivideMetricSeries(result["comparison"]?["series"], denominator["comparison"]?["series"]);
            if (J.Bool(denominator, "truncated"))
            {
                result["truncated"] = true;
                result["coverage"]!["truncated"] = true;
            }
            result["unit"] = "1";
        }
        return result;
    }
    /// <summary>
    /// The change one point records for <paramref name="function"/>: a delta point's own value, or the
    /// difference to the last recorded point. A cumulative sum restarted at its recorded start time, so
    /// its value is the change since then; a first point claims that interval only when it began inside
    /// the window. A rate divides by the interval the points recorded and is unknown where none was.
    /// https://github.com/open-telemetry/opentelemetry-proto/blob/v1.11.1/opentelemetry/proto/metrics/v1/metrics.proto
    /// </summary>
    private double? Change(MetricSeriesRow series, MetricPointRow point, MetricPointRow? previous, string function)
    {
        if (point.Value is not { } value) return null;
        var cumulativeSum = series is { MetricKind: 2, Temporality: 2 };
        double change; ulong? interval;
        if (series.Temporality == 1)
        {
            change = value;
            interval = point.StartTimeUnixNano is { } start && start < point.TimeUnixNano ? point.TimeUnixNano - start : null;
        }
        else if (previous?.Value is not { } prior)
        {
            if (function == "delta" || !cumulativeSum || point.StartTimeUnixNano is not { } start || start >= point.TimeUnixNano || start < _window.StartNs) return null;
            change = value; interval = point.TimeUnixNano - start;
        }
        else if (function == "delta") { change = value - prior; interval = point.TimeUnixNano - previous.TimeUnixNano; }
        else if (cumulativeSum && point.StartTimeUnixNano is { } start && previous.StartTimeUnixNano is { } priorStart && start != priorStart)
        {
            change = value; interval = start < point.TimeUnixNano ? point.TimeUnixNano - start : null;
        }
        // A monotonic counter that decreased without a recorded new start was reset at an unknown time.
        else if (series.IsMonotonic != 0 && value < prior) { change = value; interval = null; }
        else { change = value - prior; interval = point.TimeUnixNano - previous.TimeUnixNano; }
        return function != "rate" ? change : interval is { } elapsed and > 0 ? change / (elapsed / 1_000_000_000d) : null;
    }
    /// <summary>
    /// A histogram's count or sum per second: per series, what its delta points recorded over the time they
    /// recorded it, summed across series. A point without a recorded measurement or a known interval start
    /// leaves the rate unknown.
    /// </summary>
    private static double? HistogramRate(IEnumerable<MetricPointRow> points, string aggregation)
    {
        double? rate = null;
        foreach (var series in points.GroupBy(static p => p.SeriesId, StringComparer.Ordinal))
        {
            double measure = 0, seconds = 0;
            foreach (var point in series)
            {
                if (point.HistogramCount is not { } count || point.StartTimeUnixNano is not { } start || start >= point.TimeUnixNano) return null;
                if (aggregation == "count") measure += count;
                else if (count > 0) measure += point.HistogramSum ?? double.NaN;
                seconds += (point.TimeUnixNano - start) / 1_000_000_000d;
            }
            if (double.IsNaN(measure)) return null;
            rate = (rate ?? 0) + measure / seconds;
        }
        return rate;
    }
    private static void DivideMetricSeries(JsonNode? numerators, JsonNode? denominators)
    {
        if (numerators is not JsonArray series) return;
        var byAttributes = (denominators as JsonArray ?? []).OfType<JsonObject>()
            .ToDictionary(static s => s["attributes"]!.ToJsonString(), StringComparer.Ordinal);
        foreach (var numerator in series.OfType<JsonObject>())
        {
            var denominator = byAttributes.GetValueOrDefault(numerator["attributes"]!.ToJsonString());
            var buckets = (denominator?["buckets"] as JsonArray ?? []).OfType<JsonObject>()
                .ToDictionary(static b => J.String(b, "bucket_start")!, StringComparer.Ordinal);
            foreach (var bucket in numerator["buckets"]!.AsArray().OfType<JsonObject>())
            {
                var value = buckets.GetValueOrDefault(J.String(bucket, "bucket_start")!)?["value"]?.GetValue<double>();
                bucket["value"] = value is null or 0 || bucket["value"] is null ? null
                    : JsonValue.Create(bucket["value"]!.GetValue<double>() / value.Value);
            }
        }
    }

    private JsonObject Ci(JsonObject input)
    {
        var prefix = J.String(input, "service_prefix") ?? "qyl-ci";
        var all = _spans.Where(s => s.Service.StartsWith(prefix, StringComparison.Ordinal)).GroupBy(s => s.SessionId ?? s.TraceId!).ToList();
        bool Matches(IGrouping<string, AnalysisRecord> g) => g.Any(s => _window.Contains(s.Start) && AnalysisMatchers.Match(s, input["matchers"]))
            && (J.String(input, "branch") is not { } branch || g.Any(s => J.Text(s.Value(CollectorSemanticAttributeCatalog.VcsRefHeadName)) == branch))
            && (J.String(input, "commit") is not { } commit || g.Any(s => J.Text(s.Value(CollectorSemanticAttributeCatalog.VcsRefHeadRevision)) == commit))
            && (J.String(input, "outcome") switch { "failed" => g.Any(s => s.Error), "passed" => g.All(s => !s.Error), _ => true });
        var groups = all.Where(Matches).OrderByDescending(g => g.Max(s => s.Start)).ToList();
        var runId = J.String(input, "run_id"); var selected = runId is not null ? all.FirstOrDefault(g => g.Key == runId) : groups.FirstOrDefault();
        // Index the recorded provider runs once. Attempt numbers describe their
        // observed chronological order, rather than reporting every rerun as 1.
        var runInfo = all.ToDictionary(g => g.Key, g => new
        {
            Start = g.Min(s => s.Start), End = g.Max(s => s.End),
            ProviderId = g.Select(s => J.Text(s.Value(CollectorSemanticAttributeCatalog.CicdPipelineRunId))).FirstOrDefault(v => v.Length > 0),
            ProviderUrl = g.Select(s => J.Text(s.Value(CollectorSemanticAttributeCatalog.CicdPipelineRunUrlFull))).FirstOrDefault(v => v.Length > 0)
        }, StringComparer.Ordinal);
        var attempts = runInfo.Where(p => p.Value.ProviderId is not null).GroupBy(p => p.Value.ProviderId!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Value.Start).ThenBy(p => p.Key, StringComparer.Ordinal)
                .Select((p, index) => (RunId: p.Key, Attempt: index + 1)).ToArray(), StringComparer.Ordinal);
        var runs = groups.Select(g =>
        {
            ct.ThrowIfCancellationRequested();
            var info = runInfo[g.Key];
            var observed = info.ProviderId is { } provider ? attempts[provider] : [];
            return J.O(("run_id", g.Key), ("session_id", g.Key),
                (CollectorSemanticAttributeCatalog.State, now.ToUnixTimeMilliseconds() - (long)(info.End / 1_000_000) < 5000 ? "active" : "ended"),
                ("start_time", J.Time(info.Start)), ("duration_ms", (info.End - info.Start) / 1_000_000d),
                ("error_count", g.Count(s => s.Error)), ("services", g.Select(s => s.Service).Distinct()),
                ("attempt", observed.Length > 0 ? observed.First(p => p.RunId == g.Key).Attempt : (int?)null),
                ("attempts", J.A(observed.Where(p => p.RunId != g.Key).Select(p => J.O(("run_id", p.RunId), ("attempt", p.Attempt))))),
                ("provider_run_id", info.ProviderId), ("provider_run_url", info.ProviderUrl));
        });
        var runInput = input.DeepClone().AsObject(); runInput.Remove("phase_cursor");
        var result = Page(runInput, "runs", runs); result["mode"] = "live";
        if (selected is not null)
        {
            string Leg(AnalysisRecord s) => J.Text(s.Value(CollectorSemanticAttributeCatalog.CicdPipelineTaskName)) is { Length: > 0 } leg ? leg : s.Service;
            var taskSpans = selected.Where(s => s.Value(CollectorSemanticAttributeCatalog.CicdPipelineTaskName) is not null || s.Value(CollectorSemanticAttributeCatalog.CicdPipelineTaskType) is not null).ToList();
            var errorLogs = _logs.Where(l => l.Error).ToLookup(l => l.TraceId);
            var phases = taskSpans.OrderBy(s => s.Start).ThenBy(s => s.Id, StringComparer.Ordinal).Where(s => (J.String(input, "leg") is not { } leg || Leg(s) == leg) && (J.String(input, "phase") is not { } phase || s.Name == phase)).Select(s =>
            { var logs = errorLogs[s.TraceId].Where(l => l.SpanId == s.Id || l.SpanId is null).ToList(); return J.O(("leg", Leg(s)), ("phase", s.Name), ("status", s.Error ? "error" : s.Span!.StatusCode == 1 ? "ok" : "unset"), ("duration_ms", s.Duration), ("message", s.Span?.StatusMessage), ("span_id", s.Id), ("trace_ids", new[] { s.TraceId! }), ("error_log_count", logs.Count), ("error_logs", J.A(logs.Take(3).Select(l => J.O(("id", l.Id), ("body", l.Log?.Body), ("trace_id", l.TraceId), ("span_id", l.SpanId)))))); }).ToList();
            result["run_id"] = selected.Key;
            var phaseInput = input.DeepClone().AsObject(); phaseInput.Remove("cursor"); phaseInput.Remove("phase_cursor"); phaseInput["run_id"] = selected.Key;
            if (input["phase_cursor"] is { } phaseCursor) phaseInput["cursor"] = phaseCursor.DeepClone();
            _operation = "ci_log.phases";
            JsonObject phasePage;
            try { phasePage = Page(phaseInput, "phases", phases); } finally { _operation = "ci_log"; }
            result["phases"] = phasePage["phases"]?.DeepClone();
            result["next_phase_cursor"] = phasePage["next_cursor"]?.DeepClone();
            result["has_more"] = J.Bool(result, "has_more") || J.Bool(phasePage, "has_more");
            result["page_totals"] = J.O(("phases", phases.Count));
            if (result["coverage"] is JsonObject phaseCoverage) phaseCoverage["truncated"] = J.Bool(result, "has_more");
            if (J.String(input, "compare_to") is { } compare)
            {
                var baseline = compare == "last_success" ? all.Where(g => g.Key != selected.Key && g.Max(s => s.Start) < selected.Min(s => s.Start) && g.All(s => !s.Error)).OrderByDescending(g => g.Max(s => s.Start)).FirstOrDefault() : all.FirstOrDefault(g => g.Key == compare);
                var fresh = new List<JsonObject>(); var fixedItems = new List<JsonObject>(); var regressions = new List<JsonObject>();
                if (baseline is not null)
                {
                    foreach (var phase in taskSpans)
                    {
                        var old = baseline.FirstOrDefault(s => s.Name == phase.Name && Leg(s) == Leg(phase));
                        var change = J.O(("leg", Leg(phase)), ("phase", phase.Name), ("current_duration_ms", phase.Duration), ("baseline_duration_ms", old?.Duration), ("ratio", old?.Duration > 0 ? phase.Duration / old.Duration : null), ("trace_ids", new[] { phase.TraceId! }));
                        if (phase.Error && old?.Error != true) fresh.Add(change);
                        if (!phase.Error && old?.Error == true) fixedItems.Add(change);
                        if (old?.Duration > 0 && phase.Duration > Math.Max(old.Duration * 1.2, old.Duration + 10)) regressions.Add(change);
                    }
                }
                result["comparison"] = J.O(("baseline_run_id", baseline?.Key), ("new_failures", J.A(fresh)), ("fixed", J.A(fixedItems)), ("duration_regressions", J.A(regressions)));
            }
        }
        return result;
    }
}
