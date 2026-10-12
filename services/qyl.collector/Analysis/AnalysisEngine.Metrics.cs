using System.Text.Json.Nodes;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Analysis;

internal sealed partial class AnalysisEngine
{
    private static readonly string[] HistogramAggregations = ["count", "sum", "avg", "min", "max", "p50", "p90", "p95", "p99"];
    private static readonly string[] ScalarAggregations = ["avg", "min", "max", "sum", "count", "last"];
    private static string MetricKind(byte kind) => kind switch { 1 => "gauge", 2 => "sum", 3 => "histogram", 4 => "exponential_histogram", _ => "gauge" };
    private static AnalysisRecord SeriesRecord(MetricSeriesRow s) => new(s.SeriesId, null, null, null, s.ServiceName ?? "unknown_service", s.MetricName, s.LastSeenUnixNano, s.LastSeenUnixNano, false, 0, J.Attributes(s.AttributesJson), J.Attributes(s.ResourceJson));
    private List<MetricSeriesRow> SeriesRows(JsonObject input) => snapshot.Series.Where(s =>
        (J.String(input, "metric_name") is not { } name || s.MetricName == name)
        && (J.String(input, "name_prefix") is not { } prefix || s.MetricName.StartsWith(prefix, StringComparison.Ordinal))
        && (J.String(input, "service_name") is not { } service || s.ServiceName == service)
        && (J.String(input, "text") is not { } text || s.MetricName.Contains(text, StringComparison.OrdinalIgnoreCase) || s.Description?.Contains(text, StringComparison.OrdinalIgnoreCase) == true)
        && (J.String(input, "kind") is not { } kind || MetricKind(s.MetricKind) == kind)
        && (J.String(input, "unit") is not { } unit || s.Unit == unit)
        && AnalysisMatchers.Match(SeriesRecord(s), input["matchers"])).ToList();
    private JsonObject Metrics(JsonObject input)
    {
        var items = SeriesRows(input).GroupBy(static s => s.MetricName).OrderBy(static g => g.Key, StringComparer.Ordinal).Select(static g =>
        {
            var first = g.First(); var result = JsonSerializer.SerializeToNode(MetricMapper.ToContract(new MetricCatalogEntry(first.MetricName, first.MetricKind, first.Temporality, first.IsMonotonic, first.Unit, first.Description, g.Count(), g.Max(static s => s.LastSeenUnixNano))), QylSerializerContext.Default.MetricDescriptor)!.AsObject();
            result["description"] = first.Description; result["temporality"] = first.Temporality == 2 ? "cumulative" : first.Temporality == 1 ? "delta" : "unspecified";
            result["monotonic"] = first.IsMonotonic != 0; result["supported_aggregations"] = J.N(first.MetricKind >= 3 ? HistogramAggregations : ScalarAggregations);
            result["services"] = J.N(g.Select(static s => s.ServiceName).OfType<string>().Distinct());
            result["conflicts"] = J.A(g.Select(static s => (s.MetricKind, s.Unit)).Distinct().Skip(1).Select(static s => J.O(("kind", MetricKind(s.MetricKind)), ("unit", s.Unit))));
            return result;
        });
        return Page(input, "items", items, 200);
    }
    private JsonObject MetricSeries(JsonObject input)
    {
        var rows = SeriesRows(input).Where(s => snapshot.Points.Any(p => p.SeriesId == s.SeriesId && _window.Contains(p.TimeUnixNano))).OrderBy(s => s.SeriesId, StringComparer.Ordinal).ToList();
        var result = Page(input, "items", rows.Select(s =>
        { var item = JsonSerializer.SerializeToNode(MetricMapper.ToContract(s), QylSerializerContext.Default.MetricSeries)!.AsObject(); item["new"] = _window.Contains(s.FirstSeenUnixNano); item["stale"] = s.LastSeenUnixNano < _window.StartNs; return item; }), 200);
        var keys = rows.SelectMany(s => SeriesRecord(s).Attributes.Select(p => (p.Key, p.Value))).GroupBy(p => p.Key).Select(g =>
        { var values = g.GroupBy(p => J.Text(p.Value)).ToList(); return J.O(("key", g.Key), ("cardinality", values.Count), ("cardinality_exact", true), ("top_values", J.A(values.OrderByDescending(v => v.Count()).Take(10).Select(v => J.O(("value", ContractValue(v.First().Value)), ("count", v.Count()), ("share", v.Count() / (double)Math.Max(1, rows.Count)))))), ("coverage_share", g.Count() / (double)Math.Max(1, rows.Count))); }).ToList();
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
        if (rows.Count == 0) throw new AnalysisInputException("metric_name", "Metric was not found in this project.", "metric.not_found");
        var aggregation = J.String(input, "aggregation") ?? "avg"; var function = J.String(input, "function") ?? "none";
        var groupBy = (input["group_by"] as JsonArray)?.Select(J.Text).ToArray() ?? [];
        var step = (long)J.Number(input, "step_ms", 60000); if (step < 1 || step > 86_400_000) throw new AnalysisInputException("step_ms", "step_ms must be 1–86400000.");
        var streams = rows.GroupBy(s => string.Join('\0', groupBy.Select(key => J.Text(SeriesRecord(s).Value(key))))).ToList();
        var output = new List<JsonObject>();
        foreach (var stream in streams)
        {
            var values = new List<(ulong Time, double? Value, MetricPointRow Point)>();
            foreach (var series in stream)
            {
                var points = snapshot.Points.Where(p => p.SeriesId == series.SeriesId).OrderBy(p => p.TimeUnixNano).ToList();
                MetricPointRow? previous = null;
                foreach (var point in points)
                {
                    double? value = point.Value;
                    if (function != "none" && series.Temporality == 1 && point.Value.HasValue)
                    {
                        value = point.Value;
                        if (function == "rate") value /= Math.Max(.001, (point.TimeUnixNano - (point.StartTimeUnixNano ?? point.TimeUnixNano)) / 1_000_000_000d);
                    }
                    else if (function != "none" && previous is not null && point.Value.HasValue && previous.Value.HasValue)
                    {
                        var reset = point.StartTimeUnixNano.HasValue && previous.StartTimeUnixNano.HasValue && point.StartTimeUnixNano != previous.StartTimeUnixNano;
                        value = function == "delta" ? point.Value - previous.Value : !reset && point.Value >= previous.Value ? point.Value - previous.Value : point.Value;
                        if (function == "rate") value /= Math.Max(.001, (point.TimeUnixNano - previous.TimeUnixNano) / 1_000_000_000d);
                    }
                    else if (function != "none") value = null;
                    if (_window.Contains(point.TimeUnixNano)) values.Add((point.TimeUnixNano, value, point));
                    previous = point;
                }
            }
            var buckets = new JsonArray();
            var bucketCount = (int)Math.Ceiling((_window.End - _window.Start).TotalMilliseconds / step);
            if (bucketCount > 10000) throw new AnalysisInputException("step_ms", "Increase step_ms so the window contains no more than 10000 buckets.");
            for (var i = 0; i < bucketCount; i++)
            {
                var start = _window.StartNs + (ulong)i * (ulong)step * 1_000_000; var end = start + (ulong)step * 1_000_000;
                var bucket = values.Where(p => p.Time >= start && p.Time < end).ToList();
                double? value;
                if (stream.First().MetricKind >= 3)
                    value = Histogram(bucket.Select(p => p.Point).ToList(), aggregation);
                else
                {
                    var numbers = bucket.Where(p => p.Value.HasValue).Select(p => p.Value!.Value).ToList();
                    value = numbers.Count == 0 ? null : function == "increase" ? numbers.Sum() : aggregation switch { "min" => numbers.Min(), "max" => numbers.Max(), "sum" => numbers.Sum(), "count" => numbers.Count, "last" => numbers[^1], _ => numbers.Average() };
                }
                buckets.Add((JsonNode)J.O(("bucket_start", J.Time(start)), ("value", value), ("point_count", bucket.Count)));
            }
            var attributes = new JsonObject(); foreach (var key in groupBy) attributes[key] = SeriesRecord(stream.First()).Value(key)?.DeepClone() ?? JsonValue.Create("");
            output.Add(J.O(("attributes", ContractAttributes(attributes)), ("buckets", buckets)));
        }
        var limit = (int)J.Number(input, "series_limit", 50); if (limit is < 1 or > 500) throw new AnalysisInputException("series_limit", "Use a series_limit from 1 to 500.");
        var result = J.O(("name", name), ("kind", rows.Count == 0 ? "gauge" : MetricKind(rows[0].MetricKind)), ("unit", function == "rate" ? rows.FirstOrDefault()?.Unit + "/s" : rows.FirstOrDefault()?.Unit), ("aggregation", aggregation), ("step_ms", step), ("start_time", _window.Start.ToString("O")), ("end_time", _window.End.ToString("O")), ("series", J.A(output.Take(limit))), ("truncated", output.Count > limit),
            ("method", rows.Any(s => s.MetricKind >= 3) ? "histogram buckets merged, linear interpolation" : function == "none" ? "recorded scalar points aggregated" : "per-series counter differences across resets"), ("approximate", rows.Any(s => s.MetricKind >= 3 && aggregation.StartsWith('p'))), ("exemplar_trace_ids", Array.Empty<string>()), ("coverage", Coverage(snapshot.Points.Count, rows.Sum(s => snapshot.Points.Count(p => p.SeriesId == s.SeriesId && _window.Contains(p.TimeUnixNano))), Math.Min(output.Count, limit), output.Count > limit)));
        if (input["compare_to"] is { } compare)
        {
            var copy = input.DeepClone().AsObject(); copy.Remove("compare_to"); var current = _window; var offset = compare is JsonObject ? J.Number(compare, "offset_ms") : (_window.End - _window.Start).TotalMilliseconds;
            _window = new(_window.Start.AddMilliseconds(-offset), _window.End.AddMilliseconds(-offset)); var comparison = QueryMetric(copy); result["comparison"] = J.O(("window", _window.Json), ("series", comparison["series"])); _window = current;
        }
        if (input["ratio_of"] is JsonObject ratio)
        {
            var copy = input.DeepClone().AsObject(); copy.Remove("ratio_of"); copy["metric_name"] = ratio["metric_name"]?.DeepClone(); copy["aggregation"] = ratio["aggregation"]?.DeepClone() ?? JsonValue.Create(aggregation); copy["matchers"] = ratio["matchers"]?.DeepClone();
            var denominator = QueryMetric(copy);
            for (var i = 0; i < ((JsonArray)result["series"]!).Count; i++)
                for (var b = 0; b < ((JsonArray)result["series"]![i]!["buckets"]!).Count; b++)
                { var n = result["series"]![i]!["buckets"]![b]!; var d = denominator["series"] is JsonArray ds && i < ds.Count ? ds[i]?["buckets"]?[b]?["value"] : null; var value = d is null ? 0 : d.GetValue<double>(); n["value"] = value == 0 || n["value"] is null ? null : JsonValue.Create(n["value"]!.GetValue<double>() / value); }
            result["unit"] = "1";
        }
        return result;
    }
    private static double? Histogram(List<MetricPointRow> points, string aggregation)
    {
        if (points.Count == 0) return null;
        var count = points.Sum(static p => (double)(p.HistogramCount ?? 0)); var sum = points.Sum(static p => p.HistogramSum ?? 0);
        if (aggregation == "count") return count;
        if (aggregation == "sum") return sum;
        if (aggregation == "avg") return count == 0 ? null : sum / count;
        if (aggregation == "min") return points.Min(static p => p.HistogramMin);
        if (aggregation == "max") return points.Max(static p => p.HistogramMax);
        if (!double.TryParse(aggregation.AsSpan(1), CultureInfo.InvariantCulture, out var percentile)) throw new AnalysisInputException("aggregation", "Choose a supported histogram aggregation.");
        var merged = new SortedDictionary<double, double>();
        foreach (var point in points)
        {
            var bounds = JsonNode.Parse(point.BucketBoundsJson ?? "[]")!.AsArray().Select(static v => v!.GetValue<double>()).ToArray();
            var counts = JsonNode.Parse(point.BucketCountsJson ?? "[]")!.AsArray().Select(static v => v!.GetValue<double>()).ToArray();
            for (var i = 0; i < counts.Length; i++) { var bound = i < bounds.Length ? bounds[i] : double.PositiveInfinity; merged[bound] = merged.GetValueOrDefault(bound) + counts[i]; }
        }
        var target = count * percentile / 100; double cumulative = 0, lower = 0;
        foreach (var (upper, amount) in merged)
        {
            if (cumulative + amount >= target && amount > 0)
                return double.IsInfinity(upper) ? points.Max(static p => p.HistogramMax) ?? lower : lower + (upper - lower) * (target - cumulative) / amount;
            cumulative += amount; lower = upper;
        }
        return null;
    }
    private JsonObject Ci(JsonObject input)
    {
        var prefix = J.String(input, "service_prefix") ?? "qyl-ci";
        var all = _spans.Where(s => s.Service.StartsWith(prefix, StringComparison.Ordinal)).GroupBy(s => s.SessionId ?? s.TraceId!).ToList();
        bool Matches(IEnumerable<AnalysisRecord> g) => g.Any(s => _window.Contains(s.Start) && AnalysisMatchers.Match(s, input["matchers"]))
            && (J.String(input, "branch") is not { } branch || g.Any(s => J.Text(s.Value(CollectorSemanticAttributeCatalog.VcsRefHeadName)) == branch))
            && (J.String(input, "commit") is not { } commit || g.Any(s => J.Text(s.Value(CollectorSemanticAttributeCatalog.VcsRefHeadRevision)) == commit))
            && (J.String(input, "outcome") switch { "failed" => g.Any(s => s.Error), "passed" => g.All(s => !s.Error), _ => true });
        var groups = all.Where(Matches).OrderByDescending(g => g.Max(s => s.Start)).ToList();
        var runId = J.String(input, "run_id"); var selected = runId is not null ? all.FirstOrDefault(g => g.Key == runId) : groups.FirstOrDefault();
        var runs = groups.Select(g => J.O(("run_id", g.Key), ("session_id", g.Key), (CollectorSemanticAttributeCatalog.State, g.Any(s => s.Error) ? "ended" : "ended"), ("start_time", J.Time(g.Min(s => s.Start))), ("duration_ms", (g.Max(s => s.End) - g.Min(s => s.Start)) / 1_000_000d), ("error_count", g.Count(s => s.Error)), ("services", g.Select(s => s.Service).Distinct()), ("attempt", (int)g.Max(s => J.Number(s.Attributes, "cicd.pipeline.run.attempt", 1))), ("attempts", J.A(all.Where(other => other.Key != g.Key && other.Any(o => g.Any(s => J.Text(s.Value(CollectorSemanticAttributeCatalog.CicdPipelineRunId)) is { Length: > 0 } pid && J.Text(o.Value(CollectorSemanticAttributeCatalog.CicdPipelineRunId)) == pid))).Select(other => J.O(("run_id", other.Key), ("attempt", 1))))), ("provider_run_id", g.Select(s => J.Text(s.Value(CollectorSemanticAttributeCatalog.CicdPipelineRunId))).FirstOrDefault(v => v.Length > 0)), ("provider_run_url", g.Select(s => J.Text(s.Value(CollectorSemanticAttributeCatalog.CicdPipelineRunUrlFull))).FirstOrDefault(v => v.Length > 0))));
        var result = Page(input, "runs", runs, 10, 50); result["mode"] = "live";
        if (selected is not null)
        {
            string Leg(AnalysisRecord s) => J.Text(s.Value("ci.leg")) is { Length: > 0 } leg ? leg : s.Service;
            var phases = selected.Where(s => (J.String(input, "leg") is not { } leg || Leg(s) == leg) && (J.String(input, "phase") is not { } phase || s.Name == phase)).Select(s =>
            { var logs = _logs.Where(l => l.Error && l.TraceId == s.TraceId && (l.SpanId == s.Id || l.SpanId is null)).ToList(); return J.O(("leg", Leg(s)), ("phase", s.Name), ("status", s.Error ? "error" : s.Span!.StatusCode == 1 ? "ok" : "unset"), ("duration_ms", s.Duration), ("message", s.Span?.StatusMessage), ("span_id", s.Id), ("trace_ids", new[] { s.TraceId! }), ("error_log_count", logs.Count), ("error_logs", J.A(logs.Take(3).Select(l => J.O(("id", l.Id), ("body", l.Log?.Body), ("trace_id", l.TraceId), ("span_id", l.SpanId)))))); }).ToList();
            result["run_id"] = selected.Key; result["phases"] = J.A(phases.Take((int)J.Number(input, "limit", 50)));
            if (J.String(input, "compare_to") is { } compare)
            {
                var baseline = compare == "last_success" ? all.Where(g => g.Key != selected.Key && g.Max(s => s.Start) < selected.Min(s => s.Start) && g.All(s => !s.Error)).OrderByDescending(g => g.Max(s => s.Start)).FirstOrDefault() : all.FirstOrDefault(g => g.Key == compare);
                var fresh = new List<JsonObject>(); var fixedItems = new List<JsonObject>(); var regressions = new List<JsonObject>();
                if (baseline is not null)
                {
                    foreach (var phase in selected)
                    {
                        var old = baseline.FirstOrDefault(s => s.Name == phase.Name && Leg(s) == Leg(phase));
                        var change = J.O(("leg", Leg(phase)), ("phase", phase.Name), ("current_duration_ms", phase.Duration), ("baseline_duration_ms", old?.Duration), ("ratio", old?.Duration > 0 ? phase.Duration / old.Duration : null), ("trace_ids", new[] { phase.TraceId! }));
                        if (phase.Error && old?.Error != true) fresh.Add(change);
                        if (!phase.Error && old?.Error == true) fixedItems.Add(change);
                        if (old?.Duration > 0 && phase.Duration > old.Duration) regressions.Add(change);
                    }
                }
                result["comparison"] = J.O(("baseline_run_id", baseline?.Key), ("new_failures", J.A(fresh)), ("fixed", J.A(fixedItems)), ("duration_regressions", J.A(regressions)));
            }
        }
        return result;
    }
}
