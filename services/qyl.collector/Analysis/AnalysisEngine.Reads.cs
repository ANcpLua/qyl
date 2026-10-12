using System.Text.Json.Nodes;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Analysis;

internal sealed partial class AnalysisEngine
{
    private static readonly string[] MissingArguments = ["Arguments not captured; repeats cannot be confirmed without argument hashes."];
    private JsonObject AnnotationJson(AnnotationRow row) => _state.GetValueOrDefault("annotation:" + row.AnnotationId) is { } metadata ? JsonNode.Parse(metadata.ValueJson)!.AsObject() : JsonSerializer.SerializeToNode(AnnotationMapper.ToContract(row), QylSerializerContext.Default.Annotation)!.AsObject();
    private List<JsonObject> TraceAnnotations(string traceId, IReadOnlySet<string>? spanIds = null) => snapshot.Annotations
        .Where(note => note.TraceId == traceId && (note.SpanId is null || spanIds is null || spanIds.Contains(note.SpanId)))
        .Select(AnnotationJson).ToList();
    private static IEnumerable<string> AnnotationLabels(IEnumerable<JsonObject> notes) => notes
        .SelectMany(static note => (note["labels"] as JsonArray)?.Select(J.Text) ?? [])
        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
    private static JsonObject LogJson(AnalysisRecord row)
    {
        var result = JsonSerializer.SerializeToNode(LogMapper.ToContract(row.Log!), QylSerializerContext.Default.LogRecord)!.AsObject();
        result["log_id"] = row.Id;
        return result;
    }
    private static JsonObject ContractSpan(AnalysisRecord row) => JsonSerializer.SerializeToNode(SpanMapper.ToContracts([row.Span!])[0], QylSerializerContext.Default.Span)!.AsObject();
    private static JsonObject Tokens(IEnumerable<AnalysisRecord> rows)
    {
        var spans = rows.Where(s => s.Span is not null).Select(s => s.Span!).ToList();
        long? Sum(Func<SpanStorageRow, long?> select) => spans.Any(s => select(s).HasValue) ? spans.Sum(s => select(s) ?? 0) : null;
        return J.O(("input", Sum(s => s.GenAiInputTokens)), ("output", Sum(s => s.GenAiOutputTokens)), ("cache_read", Sum(s => s.GenAiCacheReadInputTokens)), ("cache_write", Sum(s => s.GenAiCacheWriteInputTokens)), ("reasoning", Sum(s => s.GenAiReasoningTokens)));
    }
    private static long TotalTokens(IEnumerable<AnalysisRecord> rows) => rows.Sum(static r => (r.Span?.GenAiInputTokens ?? 0) + (r.Span?.GenAiOutputTokens ?? 0));
    private JsonObject AgentRun(JsonObject input)
    {
        var rows = Execution(input); var limit = (int)J.Number(input, "max_steps", 100);
        if (limit is < 1 or > 1000) throw new AnalysisInputException("max_steps", "max_steps must be 1 to 1000.");
        var steps = rows.Where(r => r.Span?.GenAiOperationName is not null || r.Value(CollectorSemanticAttributeCatalog.McpMethodName) is not null || r.Value(CollectorSemanticAttributeCatalog.GenAiToolName) is not null).Select((r, i) =>
        {
            var kind = r.Value(CollectorSemanticAttributeCatalog.GenAiToolName) is not null || r.Value(CollectorSemanticAttributeCatalog.McpMethodName) is not null ? "tool_call" : r.Span?.GenAiOperationName is "invoke_agent" or "create_agent" ? "agent" : r.Span?.GenAiOperationName == "retrieval" ? "retrieval" : "llm_call";
            return J.O(("index", i), ("kind", kind), ("name", r.Name), ("mcp_method", r.Value(CollectorSemanticAttributeCatalog.McpMethodName)), ("mcp_server", r.Value("mcp.server.name")), ("duration_ms", r.Duration), ("tokens", Tokens([r])), ("status", r.Error ? "ERROR" : "OK"), ("error", r.Error ? Message(r) : null), ("trace_id", r.TraceId), ("span_id", r.Id));
        }).ToList();
        var repeated = rows.Select(r => (Producer: r.Service, Tool: J.Text(r.Value(CollectorSemanticAttributeCatalog.GenAiToolName)), Hash: J.Text(r.Value(CollectorSemanticAttributeCatalog.GenAiToolCallArgumentsHash))))
            .Where(r => r.Tool.Length > 0 && r.Hash.Length > 0).GroupBy(r => r).Where(g => g.Count() > 1).Select(g => J.O(("tool", g.Key.Tool), ("argument_hash", g.Key.Hash), ("count", g.Count())));
        var total = TotalTokens(rows); var start = rows.Count == 0 ? now : DateTimeOffset.FromUnixTimeMilliseconds((long)(rows.Min(r => r.Start) / 1_000_000));
        return J.O(("run", J.O(("agent_name", rows.Select(r => J.Text(r.Value(CollectorSemanticAttributeCatalog.GenAiAgentName))).FirstOrDefault(v => v.Length > 0)), ("models", rows.Select(r => r.Span?.GenAiResponseModel ?? r.Span?.GenAiRequestModel).OfType<string>().Distinct()), ("started", start.ToString("O")), ("duration_ms", rows.Count == 0 ? 0 : (rows.Max(r => r.End) - rows.Min(r => r.Start)) / 1_000_000d), ("tokens", Tokens(rows)), ("errors", rows.Count(r => r.Error)), ("session_id", J.String(input, "session_id")), ("trace_id", J.String(input, "trace_id")))),
            ("steps", J.A(steps)), ("findings", J.O(("repeated_calls", J.A(repeated)), ("failed_tool_calls", J.A(steps.Where(s => J.String(s, "kind") == "tool_call" && J.String(s, "status") == "ERROR").Take(10))), ("slowest_steps", J.A(steps.OrderByDescending(s => J.Number(s, "duration_ms")).Take(3))), ("token_share", J.A(rows.GroupBy(r => r.Name).Select(g => J.O(("name", g.Key), ("tokens", TotalTokens(g)), ("share", total == 0 ? 0 : TotalTokens(g) / (double)total))))))),
            ("unknowns", J.N(rows.Any(r => r.Value(CollectorSemanticAttributeCatalog.GenAiToolName) is not null && r.Value(CollectorSemanticAttributeCatalog.GenAiToolCallArgumentsHash) is null) ? MissingArguments : Array.Empty<string>())),
            ("coverage", Coverage(rows.Count, steps.Count, Math.Min(steps.Count, limit), steps.Count > limit)));
    }
    private JsonObject Sessions(JsonObject input)
    {
        var all = _spans.Where(s => s.SessionId is not null).GroupBy(s => s.SessionId!).Select(g =>
        {
            var items = g.ToList(); var start = items.Min(s => s.Start); var end = items.Max(s => s.End); var errors = items.Count(s => s.Error);
            var model = items.Select(s => s.Span?.GenAiResponseModel ?? s.Span?.GenAiRequestModel).OfType<string>().Distinct().ToArray();
            var active = now.ToUnixTimeMilliseconds() - (long)(end / 1_000_000) < 300_000;
            var annotations = snapshot.Annotations.Where(n => n.SessionId == g.Key).Select(AnnotationJson).ToList();
            var labels = annotations.SelectMany(n => (n["labels"] as JsonArray)?.Select(J.Text) ?? []);
            var name = items.Select(s => J.Text(s.Value(CollectorSemanticAttributeCatalog.GenAiAgentName))).FirstOrDefault(v => v.Length > 0);
            if (!_window.Contains(start) || J.Bool(input, "active_only") && !active || input["has_error"] is not null && J.Bool(input, "has_error") != (errors > 0)
                || errors < J.Number(input, "min_errors") || (end - start) / 1_000_000d < J.Number(input, "min_duration_ms") || TotalTokens(items) < J.Number(input, "min_tokens")
                || J.String(input, "agent_name") is { } agent && name != agent || J.String(input, "model") is { } requested && !model.Contains(requested)
                || J.String(input, "annotation_label") is { } label && !labels.Contains(label)
                || !items.Any(s => AnalysisMatchers.Match(s, input["matchers"]) && (J.String(input, "service_name") is not { } service || s.Service == service))) return null;
            var byModel = new JsonObject(); foreach (var m in model) byModel[m] = Tokens(items.Where(s => (s.Span?.GenAiResponseModel ?? s.Span?.GenAiRequestModel) == m));
            return J.O(("session_id", g.Key), (CollectorSemanticAttributeCatalog.State, active ? "active" : "ended"), ("start_time", J.Time(start)), ("end_time", J.Time(end)), ("duration_ms", (end - start) / 1_000_000d), ("last_activity", J.Time(end)), ("trace_count", items.Select(s => s.TraceId).Distinct().Count()), ("span_count", items.Count), ("error_count", errors), ("services", items.Select(s => s.Service).Distinct()), ("genai_usage", J.O(("request_count", items.Count(s => s.Span?.GenAiProviderName is not null)), ("total_input_tokens", TotalInput(items)), ("total_output_tokens", TotalOutput(items)), ("models_used", model), ("providers_used", items.Select(s => s.Span?.GenAiProviderName).OfType<string>().Distinct()))), ("failure_ids", items.Where(s => s.Error).Select(s => FailureId(Signature(s))).Distinct().Take(5)), ("agent_name", name), ("models", model), ("tokens_by_model", byModel), ("annotations", J.A(annotations)), ("tokens", TotalTokens(items)));
        }).OfType<JsonObject>();
        var sorted = J.String(input, "sort_by") switch { "errors" => all.OrderByDescending(s => J.Number(s, "error_count")), "duration" => all.OrderByDescending(s => J.Number(s, "duration_ms")), "tokens" => all.OrderByDescending(s => J.Number(s, "tokens")), _ => all.OrderByDescending(s => J.String(s, "last_activity"), StringComparer.Ordinal) };
        var result = Page(input, "sessions", sorted.ThenBy(s => J.String(s, "session_id"), StringComparer.Ordinal), 20, 100); result["mode"] = "live"; var sessionIds = result["sessions"]!.AsArray().Select(static item => J.String(item, "session_id")).OfType<string>().ToHashSet(StringComparer.Ordinal);
        result["annotations"] = J.A(snapshot.Annotations.Where(note => note.SessionId is not null && sessionIds.Contains(note.SessionId)).Select(AnnotationJson)); return result;
    }
    private static long TotalInput(IEnumerable<AnalysisRecord> rows) => rows.Sum(static s => s.Span?.GenAiInputTokens ?? 0);
    private static long TotalOutput(IEnumerable<AnalysisRecord> rows) => rows.Sum(static s => s.Span?.GenAiOutputTokens ?? 0);
    private JsonObject Traces(JsonObject input)
    {
        var traces = _spans.GroupBy(s => s.TraceId!).Where(g =>
        {
            var groupRows = g.ToList();
            var root = groupRows.OrderBy(s => Depth(s, groupRows)).ThenBy(s => s.Start).First(); var duration = (g.Max(s => s.End) - g.Min(s => s.Start)) / 1_000_000d;
            return _window.Contains(g.Min(s => s.Start)) && (!J.Bool(input, "errors_only") || (J.String(input, "error_scope") == "root" ? root.Error : g.Any(s => s.Error)))
                && (J.String(input, "session_id") is not { } session || g.Any(s => s.SessionId == session))
                && (J.String(input, "trace_id") is not { } trace || g.Key == trace)
                && (J.String(input, "root_span_name") is not { } name || root.Name == name)
                && (J.String(input, "server_name") is not { } mcpServer || g.Any(s => J.Text(s.Value("mcp.server.name")) == mcpServer || s.Service == mcpServer))
                && (J.String(input, "tool_name") is not { } toolName || g.Any(s => J.Text(s.Value(CollectorSemanticAttributeCatalog.GenAiToolName)) == toolName))
                && (J.String(input, "transport") is not { } transport || g.Any(s => J.Text(s.Value("mcp.transport")) == transport || J.Text(s.Value(CollectorSemanticAttributeCatalog.NetworkTransport)) == transport))
                && (J.String(input, "failure_class") is not { } failureClass || g.Any(s => s.Error && McpFailureClass(s) == failureClass))
                && duration >= J.Number(input, "min_duration_ms") && duration <= J.Number(input, "max_duration_ms", double.MaxValue)
                && (J.String(input, "failure_id") is not { } fid || g.Any(s => s.Error && FailureId(Signature(s)) == fid))
                && (J.String(input, "annotation_label") is not { } label || AnnotationLabels(TraceAnnotations(g.Key)).Contains(label, StringComparer.Ordinal))
                && g.Any(s => AnalysisMatchers.Match(s, input["matchers"]) && (J.String(input, "service_name") is not { } service || s.Service == service) && (J.String(input, "release") is not { } release || J.Text(s.Value(CollectorSemanticAttributeCatalog.ServiceVersion, "resource")) == release));
        }).Select(g =>
        {
            var items = g.ToList(); var root = items.OrderBy(s => Depth(s, items)).ThenBy(s => s.Start).First();
            return J.O(("trace_id", g.Key), ("root_span", ContractSpan(root)), ("duration_ns", (ulong)(g.Max(s => s.End) - g.Min(s => s.Start))), ("services", g.Select(s => s.Service).Distinct()), ("has_error", g.Any(s => s.Error)), ("start_time", J.Time(g.Min(s => s.Start))), ("end_time", J.Time(g.Max(s => s.End))), ("duration_ms", (g.Max(s => s.End) - g.Min(s => s.Start)) / 1_000_000d), ("span_count", g.Count()), ("error_count", g.Count(s => s.Error)), ("root_span_name", root.Name), ("root_service_name", root.Service), ("service_names", g.Select(s => s.Service).Distinct()), ("root_error", root.Error), ("contained_error", g.Any(s => s.Error)), ("failure_ids", g.Where(s => s.Error).Select(s => FailureId(Signature(s))).Distinct().Take(3)), ("annotation_labels", AnnotationLabels(TraceAnnotations(g.Key))));
        }).OrderByDescending(t => J.String(t, "start_time"), StringComparer.Ordinal).ThenBy(t => J.String(t, "trace_id"), StringComparer.Ordinal).ToList();
        var result = Page(input, "traces", traces, 20, _operation == "fetch_telemetry" ? 200 : 100); result["mode"] = "live";
        if (_operation is "display_traces" or "fetch_telemetry")
        {
            foreach (var item in result["traces"]!.AsArray().OfType<JsonObject>())
            {
                var detail = Trace(J.O(("trace_id", item["trace_id"])));
                item["spans"] = detail["trace"]!["spans"]!.DeepClone();
            }
            if (J.String(input, "trace_id") is { } selected) result["selected_trace_id"] = selected;
            if (J.String(input, "selected_span_id") is { } selectedSpan) result["selected_span_id"] = selectedSpan;
            if (J.String(input, "compare_to") is { } comparison && J.String(input, "trace_id") is { } current)
                result["comparison"] = CompareTraces(J.O(("a", J.O(("trace_id", current))), ("b", J.O(("trace_id", comparison)))));
        }
        if (J.String(input, "group_by") is { } by)
            result["groups"] = J.A(traces.GroupBy(t => by == "failure_id" ? J.Text(t["failure_ids"]?[0]) : J.String(t, by == "service" ? "root_service_name" : "root_span_name") ?? "").Select(g => J.O(("key", g.Key), ("count", g.Count()), ("example_trace_ids", g.Select(t => J.String(t, "trace_id")!).Take(3)))));
        return result;
    }
    private JsonObject Trace(JsonObject input)
    {
        var rows = Execution(input); var selection = J.String(input, "selection") ?? (J.Bool(input, "errors_only") ? "errors" : "all");
        if (J.Bool(input, "errors_only") && selection != "errors") throw new AnalysisInputException("selection", "errors_only=true requires selection=errors.");
        var matched = rows.Where(s => selection is "all" or "critical_path" || s.Error).ToHashSet(); var selected = matched.ToHashSet();
        var children = rows.ToLookup(s => s.Span?.ParentSpanId ?? "", StringComparer.Ordinal);
        if (selection == "errors_with_ancestors") foreach (var s in matched)
        { var current = s; while (current.Span?.ParentSpanId is { } parent && SpanIndex.GetValueOrDefault((current.TraceId, parent)) is { } ancestor && selected.Add(ancestor)) current = ancestor; }
        if (selection == "critical_path" && rows.Count > 0)
        {
            selected.Clear(); var current = rows.OrderBy(s => Depth(s, rows)).ThenBy(s => s.Start).First(); selected.Add(current);
            while (children[current.Id].OrderByDescending(s => s.Duration).FirstOrDefault() is { } child && selected.Add(child)) current = child;
        }
        var page = selected.OrderBy(s => s.Start).ThenBy(s => s.Id, StringComparer.Ordinal).ToList();
        var roles = page.Select(s =>
        {
            var role = J.O(("span_id", s.Id), ("role", matched.Contains(s) ? "matched" : "context"));
            if (selection == "critical_path") { var intervals = children[s.Id].Select(c => (Start: Math.Max(c.Start, s.Start), End: Math.Min(c.End, s.End))).Where(x => x.End > x.Start).OrderBy(x => x.Start).ToList(); ulong total = 0, end = 0; foreach (var interval in intervals) { total += interval.End - Math.Max(interval.Start, Math.Min(end, interval.End)); end = Math.Max(end, interval.End); } var self = Math.Max(0, s.Duration - total / 1_000_000d); role["self_time_ms"] = self; role["self_time_share"] = s.Duration == 0 ? 0 : self / s.Duration; }
            return role;
        });
        var spans = page.Select(s => { var span = ContractSpan(s); if (!J.Bool(input, "include_attributes", true)) { StripAttributes(span); } else if (input["attributes"] is JsonArray attrs && span["attributes"] is JsonArray values) span["attributes"] = J.A(values.Where(v => attrs.Any(a => J.Text(a) == J.String(v, "key")))); return span; }).ToList();
        var roleList = roles.ToList();
        for (var i = 0; i < spans.Count; i++) foreach (var pair in roleList[i]) if (pair.Key != "span_id") spans[i][pair.Key] = pair.Value?.DeepClone();
        var trace = J.O(("trace_id", J.String(input, "trace_id") ?? rows.FirstOrDefault()?.TraceId ?? ""), ("spans", J.A(spans)), ("span_count", rows.Count), ("duration_ns", rows.Count == 0 ? 0UL : rows.Max(s => s.End) - rows.Min(s => s.Start)), ("start_time", rows.Count == 0 ? now.ToString("O") : J.Time(rows.Min(s => s.Start))), ("end_time", rows.Count == 0 ? now.ToString("O") : J.Time(rows.Max(s => s.End))), ("services", rows.Select(s => s.Service).Distinct()), ("has_error", rows.Any(s => s.Error)));
        var links = rows.SelectMany(s => JsonNode.Parse(s.Span?.LinksJson ?? "[]")?.AsArray() ?? []).OfType<JsonObject>().Select(l => { var traceId = J.String(l, "trace_id") ?? J.String(l, "traceId"); var spanId = J.String(l, "span_id") ?? J.String(l, "spanId"); return J.O(("trace_id", traceId), ("span_id", spanId), ("available", SpanIndex.ContainsKey((traceId, spanId ?? "")))); });
        var traceIds = rows.Select(s => s.TraceId).ToHashSet(StringComparer.Ordinal);
        return J.O(("trace", trace), ("mode", "live"), ("annotations", J.A(TraceAnnotations(J.String(trace, "trace_id")!, rows.Select(static row => row.Id).ToHashSet(StringComparer.Ordinal)))), ("span_roles", J.A(roleList)), ("omitted", J.O(("matched", matched.Count - page.Count(matched.Contains)), ("context", selected.Count - matched.Count - page.Count(s => !matched.Contains(s))))),
            ("missing_parent_span_ids", rows.Where(s => s.Span?.ParentSpanId is { Length: > 0 } p && !SpanIndex.ContainsKey((s.TraceId, p))).Select(s => s.Span!.ParentSpanId!).Distinct()), ("links", J.A(links)),
            ("logs", J.A(J.Bool(input, "include_logs") || J.Number(input, "include_logs") > 0 ? _logs.Where(l => traceIds.Contains(l.TraceId)).Take((int)Math.Min(50, J.Number(input, "include_logs", 50))).Select(LogJson) : [])),
            ("settled", rows.Count == 0 || now.ToUnixTimeMilliseconds() - (long)(rows.Max(s => s.End) / 1_000_000) >= 5000), ("coverage", Coverage(rows.Count, selected.Count, page.Count, page.Count < selected.Count)));
    }
    private static void StripAttributes(JsonNode node)
    {
        if (node is JsonObject obj) { obj.Remove("attributes"); foreach (var child in obj.Select(static p => p.Value).OfType<JsonNode>().ToList()) StripAttributes(child); }
        else if (node is JsonArray list) foreach (var child in list.OfType<JsonNode>()) StripAttributes(child);
    }
    private JsonObject Logs(JsonObject input)
    {
        var rows = Select(input, "logs").Where(l => (J.String(input, "trace_id") is not { } trace || l.TraceId == trace) && (J.String(input, "span_id") is not { } span || l.SpanId == span) && (J.String(input, "session_id") is not { } session || l.SessionId == session)
            && (l.Log?.SeverityNumber ?? 0) >= J.Number(input, "severity_min") && (J.String(input, "query") is not { } query || LogBody(l).Contains(query, StringComparison.OrdinalIgnoreCase))).OrderByDescending(l => l.Start).ThenBy(l => l.Id, StringComparer.Ordinal).ToList();
        var context = (int)J.Number(input, "context"); if (context is < 0 or > 10) throw new AnalysisInputException("context", "context must be 0 to 10.");
        var result = Page(input, "logs", rows.Select(LogJson), 100); result["mode"] = "live"; result["total_matched"] = rows.Count;
        if (J.String(input, "group_by") == "pattern") result["patterns"] = J.A(rows.GroupBy(l => J.Normalize(LogBody(l))).Select(g => J.O(("template", g.Key), ("count", g.Count()), ("severity", g.Max(l => l.Log!.SeverityNumber)), ("first_seen", J.Time(g.Min(l => l.Start))), ("last_seen", J.Time(g.Max(l => l.Start))), ("example_ids", g.Select(l => l.TraceId).OfType<string>().Distinct().Take(3)))));
        if (context > 0 && result["logs"] is JsonArray page)
        {
            var seen = page.Select(p => J.String(p, "log_id")).OfType<string>().ToHashSet(); var extra = new List<JsonObject>();
            foreach (var match in rows.Where(r => seen.Contains(r.Id)))
            { var resource = _logs.Where(l => JsonNode.DeepEquals(l.Resource, match.Resource) && l.Service == match.Service).ToList(); var index = resource.IndexOf(match); extra.AddRange(resource.Skip(Math.Max(0, index - context)).Take(context * 2 + 1).Where(l => seen.Add(l.Id)).Select(LogJson)); }
            result["context_logs"] = J.A(extra);
        }
        foreach (var field in new[] { "logs", "context_logs" })
            if (result[field] is JsonArray entries) foreach (var log in entries.OfType<JsonObject>()) log.Remove("log_id");
        return result;
    }
}
