using System.Text.Json.Nodes;

namespace Qyl.Collector.Analysis;

internal sealed partial class AnalysisEngine
{
    private JsonObject Aggregate(JsonObject input)
    {
        var signal = J.String(input, "signal") ?? "spans"; var rows = Select(input, signal);
        var keys = (input["group_by"] as JsonArray)?.Select(J.Text).ToArray() ?? [];
        if (keys.Length > 4) throw new AnalysisInputException("group_by", "Group by at most four keys.");
        var measures = (input["measures"] as JsonArray)?.Select(J.Text).ToArray() ?? ["count"];
        var valid = new[] { "count", "error_count", "error_rate", "duration_avg", "duration_p50", "duration_p90", "duration_p95", "duration_p99", "duration_max" };
        if (measures.Any(m => !valid.Contains(m) && !m.StartsWith("sum:", StringComparison.Ordinal))) throw new AnalysisInputException("measures", "Choose documented count, error, duration or sum:<attribute> measures.");
        JsonObject Measure(IEnumerable<AnalysisRecord> source)
        {
            var values = source.ToList(); var result = new JsonObject();
            foreach (var m in measures)
            {
                double? value = m switch
                {
                    "count" => values.Count, "error_count" => values.Count(s => s.Error), "error_rate" => values.Count is 0 ? null : values.Count(s => s.Error) / (double)values.Count,
                    "duration_avg" => values.Count is 0 ? null : values.Average(s => s.Duration), "duration_max" => values.Count is 0 ? null : values.Max(s => s.Duration),
                    "duration_p50" => J.Percentile(values.Select(s => s.Duration), .50), "duration_p90" => J.Percentile(values.Select(s => s.Duration), .90), "duration_p95" => J.Percentile(values.Select(s => s.Duration), .95), "duration_p99" => J.Percentile(values.Select(s => s.Duration), .99),
                    _ => values.Sum(s => double.TryParse(J.Text(s.Value(m[4..])), CultureInfo.InvariantCulture, out var n) ? n : 0)
                };
                result[m] = J.N(value);
            }
            return result;
        }
        var step = (long)J.Number(input, "step_ms"); if (step < 0) throw new AnalysisInputException("step_ms", "step_ms must be positive.");
        var groups = rows.GroupBy(r => string.Join('\0', keys.Select(k => J.Text(r.Value(k))))).Select(g =>
        {
            var key = new JsonObject(); foreach (var k in keys) key[k] = g.First().Value(k)?.DeepClone() ?? JsonValue.Create("");
            var result = J.O(("key", key), ("measures", Measure(g)));
            if (step > 0) result["buckets"] = J.A(g.GroupBy(r => (r.Start - _window.StartNs) / ((ulong)step * 1_000_000)).OrderBy(b => b.Key).Select(b => J.O(("start_time", J.Time(_window.StartNs + b.Key * (ulong)step * 1_000_000)), ("measures", Measure(b)))));
            return result;
        }).ToList();
        var order = J.String(input, "order_by") ?? measures[0]; var descending = !order.EndsWith(" asc", StringComparison.Ordinal); order = order.Split(' ')[0];
        groups = descending ?
            [
                .. groups.OrderByDescending(g => J.Number(g["measures"], order))
                    .ThenBy(g => g["key"]!.ToJsonString(), StringComparer.Ordinal)
            ]
            :
            [
                .. groups.OrderBy(g => J.Number(g["measures"], order))
                    .ThenBy(g => g["key"]!.ToJsonString(), StringComparer.Ordinal)
            ];
        var limit = (int)J.Number(input, "limit", 20); if (limit is < 1 or > 200) throw new AnalysisInputException("limit", "Use a limit from 1 to 200.");
        var units = new JsonObject(); foreach (var m in measures) units[m] = m.StartsWith("duration", StringComparison.Ordinal) ? "ms" : m == "error_rate" ? "1" : signal == "spans" ? "spans" : "logs";
        return J.O(("groups", J.A(groups)), ("other", J.O(("groups", Math.Max(0, groups.Count - limit)), ("count", groups.Skip(limit).Sum(g => J.Number(g["measures"], "count"))))), ("units", units), ("coverage", Coverage(signal == "logs" ? _logs.Count : _spans.Count, rows.Count, Math.Min(limit, groups.Count), groups.Count > limit)));
    }
    private static string AttributeType(JsonNode? value) => value?.GetValueKind() switch
    {
        JsonValueKind.True or JsonValueKind.False => "boolean",
        JsonValueKind.Number => long.TryParse(value.ToJsonString(), CultureInfo.InvariantCulture, out _) ? "int" : "double",
        JsonValueKind.String => "string", JsonValueKind.Array => "array", JsonValueKind.Object => "object", _ => "null"
    };
    private JsonObject Discover(JsonObject input)
    {
        var signal = J.String(input, "signal") ?? "spans"; var rows = Select(input, signal == "logs" ? "logs" : "spans");
        var attrs = rows.SelectMany(row => (signal == "resources" ? new[] { (Scope: "resource", Map: row.Resource) } : new[] { (Scope: signal == "logs" ? "log" : "span", Map: row.Attributes), (Scope: "resource", Map: row.Resource) })
            .SelectMany(map => map.Map.Select(p => (p.Key, map.Scope, p.Value)))).Where(p => (J.String(input, "key_prefix") is not { } prefix || p.Key.StartsWith(prefix, StringComparison.Ordinal)) && (J.String(input, "key") is not { } key || key == p.Key))
            .GroupBy(p => (p.Key, p.Scope)).Select(g =>
            {
                var values = g.GroupBy(p => J.Text(p.Value)).OrderByDescending(v => v.Count()).ThenBy(v => v.Key, StringComparer.Ordinal).ToList();
                return J.O(("key", g.Key.Key), ("scope", g.Key.Scope), ("type", AttributeType(g.First().Value)),
                    ("coverage_share", g.Count() / (double)Math.Max(1, rows.Count)), ("cardinality", values.Count), ("cardinality_exact", true),
                    ("top_values", J.A(values.Take(10).Select(v => J.O(("value", ContractValue(v.First().Value)), ("count", v.Count()), ("share", v.Count() / (double)Math.Max(1, rows.Count)))))), ("semconv", SemanticMetadata.Find(g.Key.Key)));
            }).OrderBy(g => J.String(g, "key"), StringComparer.Ordinal).ThenBy(g => J.String(g, "scope"), StringComparer.Ordinal).ToList();
        var limit = (int)J.Number(input, "limit", 100); if (limit is < 1 or > 1000) throw new AnalysisInputException("limit", "Use a limit from 1 to 1000.");
        return J.O(("attributes", J.A(attrs)), ("coverage", Coverage(_spans.Count + _logs.Count, rows.Count, Math.Min(limit, attrs.Count), attrs.Count > limit)));
    }
    private List<JsonObject> ReleaseList(List<AnalysisRecord> rows)
    {
        var byService = rows.ToLookup(s => s.Service, StringComparer.Ordinal);
        return
        [
            .. rows.Where(s => J.Text(s.Value(CollectorSemanticAttributeCatalog.ServiceVersion, "resource")).Length > 0)
                .GroupBy(s => (s.Service,
                    Version: J.Text(s.Value(CollectorSemanticAttributeCatalog.ServiceVersion, "resource")),
                    Env: J.Text(s.Value(CollectorSemanticAttributeCatalog.DeploymentEnvironmentName, "resource"))))
                .Select(g =>
                {
                    var first = g.Min(s => s.Start);
                    var last = g.Max(s => s.Start);
                    var serviceRows = byService[g.Key.Service].ToList();
                    var before = serviceRows.Where(s => s.Start < first).ToList();
                    var after = serviceRows.Where(s =>
                            s.Start >= first &&
                            J.Text(s.Value(CollectorSemanticAttributeCatalog.ServiceVersion, "resource")) ==
                            g.Key.Version)
                        .ToList();
                    var previousFailures =
                        before.Where(s => s.Error).Select(Signature).ToHashSet(StringComparer.Ordinal);
                    var failures = after.Where(s => s.Error).GroupBy(Signature)
                        .Where(f => !previousFailures.Contains(f.Key)).Select(f => FailureId(f.Key));
                    var concurrent = serviceRows.Any(s =>
                        J.Text(s.Value(CollectorSemanticAttributeCatalog.ServiceVersion, "resource")) !=
                        g.Key.Version && s.Start >= first && s.Start <= last);
                    return J.O(("service", g.Key.Service), ("environment", g.Key.Env), ("version", g.Key.Version),
                        ("revision",
                            g.Select(s =>
                                    J.Text(s.Value(CollectorSemanticAttributeCatalog.VcsRefHeadRevision, "resource")))
                                .FirstOrDefault(s => s.Length > 0)),
                        ("first_seen", J.Time(first)), ("last_seen", J.Time(last)),
                        ("current", last == serviceRows.Max(s => s.Start) || concurrent), ("concurrent", concurrent),
                        ("traffic_share", g.Count() / (double)serviceRows.Count),
                        ("before", Perf(before)), ("after", Perf(after)), ("new_failure_ids", failures));

                    JsonObject Perf(List<AnalysisRecord> items) =>
                        J.O(("error_rate", items.Count is 0 ? null : items.Count(s => s.Error) / (double)items.Count),
                            ("p95_ms", J.Percentile(items.Select(s => s.Duration), .95)));
                }).OrderByDescending(r => J.String(r, "first_seen"), StringComparer.Ordinal)
        ];
    }
    private JsonObject Releases(JsonObject input) => Page(input, "releases", ReleaseList(Select(input)), 100);
    private JsonObject ServiceMap(JsonObject input)
    {
        var mapInput = input.DeepClone().AsObject(); mapInput.Remove("service_name");
        var rows = Select(mapInput); var edges = new List<(string From, string To, AnalysisRecord Span)>(); var kinds = new Dictionary<string, string>();
        foreach (var s in rows)
        {
            ct.ThrowIfCancellationRequested();
            kinds[s.Service] = "service";
            var parent = SpanIndex.GetValueOrDefault((s.TraceId, s.Span?.ParentSpanId ?? ""));
            if (parent is not null && parent.Service != s.Service) edges.Add((parent.Service, s.Service, s));
            if (J.Bool(input, "include_external", true) && s.Span?.Kind is 3 or 4 or 5)
            {
                var db = J.Text(s.Value(CollectorSemanticAttributeCatalog.DbSystemName)); var peer = J.Text(s.Value(CollectorSemanticAttributeCatalog.PeerService)); var server = J.Text(s.Value(CollectorSemanticAttributeCatalog.ServerAddress));
                var messaging = J.Text(s.Value(CollectorSemanticAttributeCatalog.MessagingSystem));
                var target = peer.Length > 0 ? peer : server.Length > 0 ? server : db.Length > 0 ? db : messaging;
                if (target.Length > 0 && target != s.Service) { kinds[target] = db.Length > 0 ? "database" : messaging.Length > 0 ? "messaging" : "external"; edges.Add((s.Service, target, s)); }
            }
        }
        if (J.String(input, "service_name") is { } focus)
        {
            var names = new HashSet<string> { focus }; var depth = (int)J.Number(input, "depth", 1);
            if (depth is < 1 or > 3) throw new AnalysisInputException("depth", "depth must be from 1 to 3.");
            for (var i = 0; i < depth; i++) { var next = edges.Where(e => names.Contains(e.From) || names.Contains(e.To)).SelectMany(e => new[] { e.From, e.To }).ToList(); names.UnionWith(next); }
            edges = [.. edges.Where(e => names.Contains(e.From) && names.Contains(e.To))]; kinds = kinds.Where(p => names.Contains(p.Key)).ToDictionary();
        }
        var grouped = edges.GroupBy(e => (e.From, e.To));
        var resultEdges = grouped.Select(g => J.O(("from", g.Key.From), ("to", g.Key.To), ("calls", g.Count()), ("error_rate", g.Count(e => e.Span.Error) / (double)g.Count()), ("p95_ms", J.Percentile(g.Select(e => e.Span.Duration), .95)), ("example_operations", g.Select(e => e.Span.Name).Distinct().Take(3)), ("unit", "spans")));
        var serviceRowsByName = rows.ToLookup(s => s.Service, StringComparer.Ordinal);
        var incomingByName = edges.ToLookup(e => e.To, StringComparer.Ordinal);
        var nodes = kinds.Select(k => { var nodeRows = serviceRowsByName[k.Key].Concat(k.Value == "service" ? [] : incomingByName[k.Key].Select(e => e.Span)).ToList(); return J.O(("name", k.Key), ("kind", k.Value), ("calls", nodeRows.Count), ("error_rate", nodeRows.Count is 0 ? 0 : nodeRows.Count(s => s.Error) / (double)nodeRows.Count), ("p95_ms", J.Percentile(nodeRows.Select(s => s.Duration), .95)), ("unit", "spans")); });
        return J.O(("nodes", J.A(nodes)), ("edges", J.A(resultEdges)), ("unknown_callers", rows.Count is 0 ? 0 : rows.Count(s => s.Span?.ParentSpanId is { Length: > 0 } p && !SpanIndex.ContainsKey((s.TraceId, p))) / (double)rows.Count));
    }
    private JsonObject Health(JsonObject input)
    {
        var rows = Select(input); var orphans = rows.Where(s => s.Span?.ParentSpanId is { Length: > 0 } p && !SpanIndex.ContainsKey((s.TraceId, p))).ToList();
        var oldest = _spans.Select(s => s.Start).Concat(_logs.Select(s => s.Start)).DefaultIfEmpty().Min();
        var lag = snapshot.Logs.Where(l => l.ObservedTimeUnixNano is not null).Select(l => (l.ObservedTimeUnixNano!.Value - Math.Min(l.TimeUnixNano, l.ObservedTimeUnixNano.Value)) / 1_000_000d).ToList();
        return J.O(("silent_services", Silent(input)), ("orphan_spans", J.O(("count", orphans.Count), ("share", rows.Count is 0 ? 0 : orphans.Count / (double)rows.Count), ("services", orphans.Select(s => s.Service).Distinct()), ("unit", "spans"))),
            ("semconv_gaps", SemanticGaps(rows)), ("unknown_service_share", rows.Count is 0 ? 0 : rows.Count(s => s.Service.StartsWith("unknown", StringComparison.Ordinal)) / (double)rows.Count),
            ("schema_urls", rows.Select(s => s.Span?.SchemaUrl).OfType<string>().Where(static url => url.Length > 0).Distinct()), ("retention", J.O(("oldest", oldest is 0 ? null : J.Time(oldest)), ("days", oldest is 0 ? null : (now.ToUnixTimeMilliseconds() - (long)(oldest / 1_000_000)) / 86_400_000d))),
            ("ingestion_lag_ms", lag.Count is 0 ? null : lag.Average()));
    }
    private static JsonArray SemanticGaps(List<AnalysisRecord> rows)
    {
        var gaps = new List<JsonObject>();
        foreach (var rule in SemanticMetadata.SpanRequirements.OfType<JsonObject>())
        {
            var type = J.String(rule, "type")!; var separator = type.LastIndexOf('.');
            var prefix = separator > 0 ? type[..separator] : type;
            var kind = J.String(rule, "kind") switch { "internal" => 1, "server" => 2, "client" => 3, "producer" => 4, "consumer" => 5, _ => 0 };
            foreach (var group in rows.Where(row => row.Span?.Kind == kind && row.Attributes.Any(a => a.Key.StartsWith(prefix + ".", StringComparison.Ordinal))).GroupBy(row => (row.Service, row.Name)))
                foreach (var required in rule["required"]!.AsArray().Select(J.Text))
                {
                    var missing = group.Count(row => row.Value(required) is null);
                    if (missing > 0) gaps.Add(J.O(("service", group.Key.Service), ("operation", group.Key.Name), ("attribute", required), ("requirement_level", "required"), ("missing_share", missing / (double)group.Count())));
                }
        }
        return J.A(gaps.DistinctBy(g => g.ToJsonString()));
    }
    private static string McpFailureClass(AnalysisRecord s)
    {
        var error = (J.Text(s.Value(CollectorSemanticAttributeCatalog.ErrorType)) + " " + s.Span?.StatusMessage).ToLowerInvariant();
        return error.Contains("timeout", StringComparison.Ordinal) ? "timeout" : error.Contains("cancel", StringComparison.Ordinal) ? "cancellation" : error.Contains("auth", StringComparison.Ordinal) || error.Contains("permission", StringComparison.Ordinal) ? "authentication" : error.Contains("transport", StringComparison.Ordinal) || error.Contains("connection", StringComparison.Ordinal) ? "transport" : J.Text(s.Value("mcp.response.is_error")) == "true" || J.Text(s.Value("mcp.is_error")) == "true" ? "tool_result" : "protocol";
    }
    private static string McpServer(AnalysisRecord s) => J.Text(s.Value("mcp.server.name")) is { Length: > 0 } value ? value : s.Service;
    private static string McpTransport(AnalysisRecord s) => J.Text(s.Value("mcp.transport")) is { Length: > 0 } value ? value : J.Text(s.Value(CollectorSemanticAttributeCatalog.NetworkTransport));
    private List<AnalysisRecord> McpRows(JsonObject input)
    {
        return
        [
            .. Select(input).Where(s => s.Value(CollectorSemanticAttributeCatalog.McpMethodName) is not null
                                        && (J.String(input, "server_name") is not { } server ||
                                            J.Text(s.Value("mcp.server.name")) == server || s.Service == server)
                                        && (J.String(input, "server_version") is not { } version ||
                                            J.Text(s.Value("mcp.server.version")) == version ||
                                            J.Text(s.Value(CollectorSemanticAttributeCatalog.ServiceVersion,
                                                "resource")) == version)
                                        && (J.String(input, "tool_name") is not { } tool ||
                                            J.Text(s.Value(CollectorSemanticAttributeCatalog.GenAiToolName)) == tool)
                                        && (J.String(input, "transport") is not { } transport ||
                                            J.Text(s.Value("mcp.transport")) == transport ||
                                            J.Text(s.Value(CollectorSemanticAttributeCatalog.NetworkTransport)) ==
                                            transport))
        ];
    }
    private JsonObject McpStats(JsonObject input)
    {
        var rows = McpRows(input);
        static JsonObject Totals(List<AnalysisRecord> items) => J.O(("requests", items.Count), ("errors", items.Count(s => s.Error)), ("error_rate", items.Count is 0 ? 0 : items.Count(s => s.Error) / (double)items.Count), ("p50_ms", J.Percentile(items.Select(s => s.Duration), .50)), ("p95_ms", J.Percentile(items.Select(s => s.Duration), .95)), ("unit", "requests"));
        var groupBy = J.String(input, "group_by") ?? "tool";
        var groups = rows.GroupBy(s => groupBy switch { "server" => McpServer(s), "method" => J.Text(s.Value(CollectorSemanticAttributeCatalog.McpMethodName)), "transport" => McpTransport(s), "failure_class" => s.Error ? McpFailureClass(s) : "success", _ => J.Text(s.Value(CollectorSemanticAttributeCatalog.GenAiToolName)) })
            .OrderByDescending(g => g.Count()).Select(g => { var result = Totals([.. g]); result["name"] = g.Key; result["example_trace_ids"] = J.N(g.OrderByDescending(s => s.Error).ThenByDescending(s => s.Duration).Select(s => s.TraceId).OfType<string>().Distinct().Take(3)); return result; }).ToList();
        var classes = new JsonObject(); foreach (var kind in new[] { "transport", "protocol", "tool_result", "authentication", "cancellation", "timeout" }) classes[kind] = rows.Count(s => s.Error && McpFailureClass(s) == kind);
        var limit = (int)J.Number(input, "limit", 100);
        var output = J.O(("totals", Totals(rows)), ("groups", J.A(groups)), ("failure_classes", classes), ("coverage", Coverage(_spans.Count, rows.Count, Math.Min(limit, groups.Count), groups.Count > limit)));
        if (input["compare_to"] is { } comparison)
        {
            var copy = input.DeepClone().AsObject(); copy.Remove("compare_to");
            var previousWindow = _window;
            if (J.Text(comparison) == "previous_window") _window = new(_window.Start - (_window.End - _window.Start), _window.Start);
            else if (comparison is JsonObject obj && obj["offset_minutes"] is not null) { var offset = J.Number(obj, "offset_minutes"); _window = new(_window.Start.AddMinutes(-offset), _window.End.AddMinutes(-offset)); }
            else copy["server_version"] = J.String(comparison, "server_version") ?? J.Text(comparison);
            var old = McpStats(copy); old["window"] = _window.Json; output["comparison"] = old; _window = previousWindow;
        }
        return output;
    }
    private JsonObject McpDashboard(JsonObject input)
    {
        var stats = McpStats(input);
        var rows = McpRows(input);
        JsonArray Counts(string key) => J.A(rows.GroupBy(s => J.Text(s.Value(key))).Select(g => J.O(("name", g.Key), ("requests", g.Count()))));
        var width = Math.Max(1000L, (long)(_window.End - _window.Start).TotalMilliseconds / 60);
        stats["window"] = J.O(("start", _window.Start.ToString("O")), ("end", _window.End.ToString("O")), ("bucket_ms", width));
        stats["buckets"] = J.A(rows.GroupBy(s => (long)(s.Start / 1_000_000) / width * width).OrderBy(g => g.Key).Select(g => J.O(("start", DateTimeOffset.FromUnixTimeMilliseconds(g.Key).ToString("O")), ("requests", g.Count()), ("errors", g.Count(s => s.Error)))));
        stats["by_server"] = Counts("mcp.server.name"); stats["by_transport"] = Counts("mcp.transport"); stats["by_method"] = Counts(CollectorSemanticAttributeCatalog.McpMethodName);
        stats["tools"] = J.A(rows.GroupBy(s => J.Text(s.Value(CollectorSemanticAttributeCatalog.GenAiToolName))).Select(g => J.O(("name", g.Key), ("requests", g.Count()), ("errors", g.Count(s => s.Error)), ("error_rate", g.Count(s => s.Error) / (double)g.Count()), ("avg_ms", g.Average(s => s.Duration)), ("p95_ms", J.Percentile(g.Select(s => s.Duration), .95)))));
        stats["span_count_analyzed"] = rows.Count; stats["truncated"] = false; stats["mode"] = "live";
        return J.O(("stats", stats), ("coverage", stats["coverage"]));
    }

}
