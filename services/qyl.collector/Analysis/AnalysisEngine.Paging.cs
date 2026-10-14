using System.Text.Json.Nodes;

namespace Qyl.Collector.Analysis;

internal sealed partial class AnalysisEngine
{
    // Each collection advances independently. The signed continuation fixes the window and
    // query, so reducing a page size never discards another collection's remaining evidence.
    private JsonObject PageCollections(JsonObject input, JsonObject result)
    {
        string[] paths = _operation switch
        {
            "triage" => ["failures", "regressions", "releases", "silent_services"],
            "get_failure_context" => ["observed.error_spans", "observed.logs", "observed.propagation", "observed.retries", "observed.fanout", "context.similar_failures", "context.changes", "context.annotations"],
            "compare_groups" => ["discriminators", "structural", "excluded_keys"],
            "compare_traces" => ["aligned", "unmatched", "after_divergence"],
            "verify_fix" => ["guardrails"],
            "aggregate_telemetry" => ["groups"],
            "discover_attributes" => ["attributes"],
            "get_agent_run" => ["steps", "findings.repeated_calls", "findings.failed_tool_calls", "findings.token_share"],
            "get_service_map" => ["edges", "nodes"],
            "check_telemetry_health" => ["silent_services", "semconv_gaps", "orphan_spans.services", "schema_urls"],
            "get_mcp_stats" => ["groups", "comparison.groups"],
            "get_trace" => ["trace.spans", "logs", "annotations", "links"],
            "fetch_telemetry" when J.String(input, "view") == "trace" => ["trace.spans", "logs", "annotations", "links"],
            _ => []
        };
        if (paths.Length == 0) { result["has_more"] ??= false; return result; }
        var scope = input.DeepClone().AsObject(); scope.Remove("cursor"); scope.Remove("limit"); scope.Remove("max_spans"); scope.Remove("max_logs"); scope.Remove("max_steps");
        // The signed token fixes its absolute window. Relative and resolved spellings
        // identify the same query, including a budget retry with a smaller page.
        foreach (var timeField in new[] { "start_time", "end_time", "lookback_minutes", "hours" }) scope.Remove(timeField);
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(scope.ToJsonString())));
        JsonObject positions = new();
        if (J.String(input, "cursor") is { } cursor)
        {
            var state = handles.Read(cursor, project, "cursor");
            if (J.String(state, "operation") != _operation || J.String(state, "filter") != fingerprint || state["positions"] is not JsonObject saved)
                throw new AnalysisInputException("cursor", "Use this cursor with its original operation and filters.", "cursor.invalid");
            positions = saved;
        }
        var more = false; var totals = new JsonObject(); var next = new JsonObject(); var returned = 0;
        foreach (var path in paths)
        {
            var parts = path.Split('.'); JsonNode? parent = result;
            foreach (var key in parts[..^1]) parent = (parent as JsonObject)?[key];
            if (parent is not JsonObject owner || owner[parts[^1]] is not JsonArray all) continue;
            var fallback = _operation == "triage" ? 10 : 100;
            var limit = (int)J.Number(input, "limit", fallback);
            if (path is "trace.spans" or "observed.error_spans") limit = Math.Min(limit, (int)J.Number(input, "max_spans", limit));
            if (path == "observed.logs") limit = Math.Min(limit, (int)J.Number(input, "max_logs", limit));
            if (path == "steps") limit = Math.Min(limit, (int)J.Number(input, "max_steps", limit));
            if (limit == 0 && path == "observed.logs") { totals[path] = all.Count; owner[parts[^1]] = new JsonArray(); continue; }
            if (limit is < 1 or > 1000) throw new AnalysisInputException("limit", "Use a page size from 1 to 1000.");
            var offset = (int)J.Number(positions, path);
            if (offset < 0 || offset > all.Count) throw new AnalysisInputException("cursor", "The collection changed; start a new query.", "cursor.expired");
            totals[path] = all.Count;
            var page = all.Skip(offset).Take(limit).ToList();
            next[path] = offset + page.Count; more |= offset + page.Count < all.Count; returned += page.Count;
            owner[parts[^1]] = J.A(page);
        }
        result["page_totals"] = totals;
        result["has_more"] = more;
        if (more) result["next_cursor"] = handles.Issue(project, "cursor", J.O(("operation", _operation), ("filter", fingerprint), ("positions", next), ("window", _window.Json)));
        else result.Remove("next_cursor");
        if (result["coverage"] is JsonObject coverage) { coverage["returned"] = returned; coverage["truncated"] = more || J.Bool(coverage, "truncated"); }
        if (result["trace"]?["spans"] is JsonArray spans)
        {
            var ids = spans.Select(s => J.String(s, "span_id")).ToHashSet(StringComparer.Ordinal);
            if (result["span_roles"] is JsonArray roles)
            {
                var omitted = roles.Where(r => !ids.Contains(J.String(r, "span_id"))).ToList();
                result["omitted"] = J.O(
                    ("matched", J.Number(result["omitted"], "matched") + omitted.Count(r => J.String(r, "role") == "matched")),
                    ("context", J.Number(result["omitted"], "context") + omitted.Count(r => J.String(r, "role") == "context")));
                result["span_roles"] = J.A(roles.Where(r => ids.Contains(J.String(r, "span_id"))));
            }
        }
        return result;
    }
}
