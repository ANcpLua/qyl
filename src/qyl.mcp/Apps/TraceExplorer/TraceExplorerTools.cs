using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using Qyl.Contracts.Primitives;
using qyl.mcp.Tools;

namespace qyl.mcp.Apps.TraceExplorer;

/// <summary>
///     MCP tools for the Trace Explorer ext-app.
///     Returns trace/span data for interactive visualization in the <c>ui://qyl/trace-viewer</c> resource.
/// </summary>
[McpServerToolType]
[QylSkill(QylSkillKind.Apps)]
internal sealed class TraceExplorerTools(HttpClient client)
{
    private const string ResourceUri = "ui://qyl/trace-viewer";

    [QylCapability("mcp_apps", QylCapabilityRole.Starting)]
    [McpServerTool(Name = "qyl.app.trace_viewer", Title = "Trace Viewer",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("""
                 Open the Trace Explorer UI for a specific trace or session.

                 Returns trace span data as a waterfall visualization in the
                 interactive Trace Explorer ext-app. Shows timing, hierarchy,
                 attributes, events, and error details for every span in the trace.

                 Provide either a trace ID (from OTLP spans) or a session ID
                 (from qyl sessions) to load the full trace waterfall.

                 Returns: Span data for UI rendering + text summary for non-UI hosts
                 """)]
    public Task<string> ViewTraceAsync(
        [Description("Trace ID to visualize")] string? traceId = null,
        [Description("Session ID to load spans from")]
        string? sessionId = null,
        [Description("Maximum spans to load (default: 200)")]
        int limit = 200) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var spans = await FetchSpansAsync(traceId, sessionId, limit).ConfigureAwait(false);

            if (spans.Count is 0)
                return "No spans found for the given trace or session.";

            var payload = BuildPayload(spans);
            var json = JsonSerializer.Serialize(payload, TraceExplorerJsonContext.Default.TraceViewerPayload);

            // Build text summary for non-UI hosts alongside the UI metadata
            StringBuilder sb = new();
            sb.AppendLine($"# Trace: {payload.Spans[0].TraceId}");
            sb.AppendLine($"Spans: {payload.Spans.Count} | Duration: {payload.TotalDurationMs:F1}ms");
            sb.AppendLine();

            var errorCount = payload.Spans.Count(s => s.Status is "ERROR");
            if (errorCount > 0)
                sb.AppendLine($"**{errorCount} error span(s) detected**");

            sb.AppendLine();
            sb.AppendLine("| Operation | Service | Duration | Status |");
            sb.AppendLine("|-----------|---------|----------|--------|");

            foreach (var span in payload.Spans.Take(30))
            {
                var statusIcon = span.Status switch
                {
                    "ERROR" => "[ERROR]",
                    "OK" => "[OK]",
                    _ => "[UNSET]"
                };
                sb.AppendLine($"| {span.OperationName} | {span.ServiceName} | {span.Duration:F1}ms | {statusIcon} |");
            }

            if (payload.Spans.Count > 30)
                sb.AppendLine($"| ... and {payload.Spans.Count - 30} more spans | | | |");

            sb.AppendLine();
            sb.AppendLine($"_meta.ui.resourceUri: {ResourceUri}");
            sb.AppendLine();
            sb.AppendLine("```json");
            sb.AppendLine(json);
            sb.AppendLine("```");

            return sb.ToString();
        });

    [QylCapability("mcp_apps", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "qyl.app.trace_search", Title = "Trace Search",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("""
                 Search for traces matching filter criteria.

                 Finds traces by service name, operation name, duration range,
                 or status. Returns a list of matching traces that can be opened
                 in the Trace Explorer UI.

                 Example queries:
                 - Errors only: trace_search(status="error")
                 - Slow requests: trace_search(minDurationMs=500)
                 - By service: trace_search(serviceName="api-gateway")

                 Returns: List of matching traces with summary statistics
                 """)]
    public Task<string> SearchTracesAsync(
        [Description("Filter by service name (partial match)")]
        string? serviceName = null,
        [Description("Filter by operation name (partial match)")]
        string? operation = null,
        [Description("Filter by status: 'ok' or 'error'")]
        string? status = null,
        [Description("Minimum span duration in milliseconds")]
        double? minDurationMs = null,
        [Description("Time window in hours (default: 24)")]
        int hours = 24,
        [Description("Maximum results (default: 50)")]
        int limit = 50) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var url = $"/api/v1/genai/spans?limit={limit}&hours={hours}";
            if (!string.IsNullOrEmpty(status))
                url += $"&status={Uri.EscapeDataString(status)}";

            var response = await client.GetFromJsonAsync(
                url, TraceExplorerJsonContext.Default.SpanSearchApiResponse).ConfigureAwait(false);

            var spans = response?.Items ?? response?.Spans ?? [];

            // Client-side filtering
            if (!string.IsNullOrEmpty(serviceName))
                spans = [.. spans.Where(s => s.ServiceName?.ContainsIgnoreCase(serviceName) is true)];

            if (!string.IsNullOrEmpty(operation))
                spans = [.. spans.Where(s => s.Name?.ContainsIgnoreCase(operation) is true)];

            if (minDurationMs is > 0)
            {
                var minNs = minDurationMs.Value * 1_000_000;
                spans = [.. spans.Where(s => s.DurationNs >= (long)minNs)];
            }

            if (spans.Count is 0)
                return "No traces found matching the criteria.";

            // Group by trace ID for summary
            var traceGroups = spans
                .GroupBy(s => s.TraceId)
                .Select(g =>
                {
                    List<SpanApiDto> traceSpans = [.. g];
                    var maxDurationMs = traceSpans.Max(s => TimeConversions.NanosToMs(s.DurationNs));
                    var hasError = traceSpans.Any(s => s.StatusCode == 2);
                    var rootOp = traceSpans
                        .OrderBy(s => s.StartTimeUnixNano)
                        .Select(s => s.Name)
                        .FirstOrDefault() ?? "unknown";

                    return new
                    {
                        TraceId = g.Key,
                        SpanCount = traceSpans.Count,
                        RootOperation = rootOp,
                        MaxDurationMs = maxDurationMs,
                        HasError = hasError,
                        Services = traceSpans.Select(s => s.ServiceName).OfType<string>().Distinct().ToList()
                    };
                })
                .OrderByDescending(t => t.MaxDurationMs)
                .Take(limit)
                .ToList();

            StringBuilder sb = new();
            sb.AppendLine($"# Trace Search Results ({traceGroups.Count} traces, {spans.Count} spans)");
            sb.AppendLine();
            sb.AppendLine("| Trace ID | Root Operation | Spans | Duration | Status | Services |");
            sb.AppendLine("|----------|----------------|-------|----------|--------|----------|");

            foreach (var trace in traceGroups)
            {
                var traceIdShort = trace.TraceId.Length > 16
                    ? trace.TraceId[..8] + "..." + trace.TraceId[^8..]
                    : trace.TraceId;
                var statusStr = trace.HasError ? "[ERROR]" : "[OK]";
                var services = string.Join(", ", trace.Services.Take(3));
                if (trace.Services.Count > 3)
                    services += $" +{trace.Services.Count - 3}";

                sb.AppendLine(
                    $"| {traceIdShort} | {trace.RootOperation} | {trace.SpanCount} | {trace.MaxDurationMs:F1}ms | {statusStr} | {services} |");
            }

            sb.AppendLine();
            sb.AppendLine(
                "Use `qyl.app.trace_viewer(traceId=\"...\")` to open a trace in the interactive Trace Explorer.");

            return sb.ToString();
        });

    private async Task<List<TraceSpanDto>> FetchSpansAsync(string? traceId, string? sessionId, int limit)
    {
        var url = !string.IsNullOrEmpty(sessionId)
            ? $"/api/v1/sessions/{Uri.EscapeDataString(sessionId)}/spans?limit={limit}"
            : $"/api/v1/genai/spans?limit={limit}";

        if (!string.IsNullOrEmpty(traceId))
            url += $"&traceId={Uri.EscapeDataString(traceId)}";

        var response = await client.GetFromJsonAsync(
            url, TraceExplorerJsonContext.Default.SpanSearchApiResponse).ConfigureAwait(false);

        var rawSpans = response?.Items ?? response?.Spans ?? [];

        // If filtering by traceId, do client-side filtering as fallback
        if (!string.IsNullOrEmpty(traceId))
            rawSpans = [.. rawSpans.Where(s => s.TraceId == traceId)];

        return [.. rawSpans.Select(MapToViewSpan)];
    }

    private static TraceViewerPayload BuildPayload(List<TraceSpanDto> spans)
    {
        var minStart = spans.Min(static s => ParseMs(s.StartTime));
        var maxEnd = spans.Max(static s => ParseMs(s.StartTime) + s.Duration);
        var totalDuration = maxEnd - minStart;

        return new TraceViewerPayload(spans, totalDuration);
    }

    private static double ParseMs(string ts) =>
        new DateTimeOffset(DateTime.Parse(ts, null, DateTimeStyles.RoundtripKind)).ToUnixTimeMilliseconds();

    private static TraceSpanDto MapToViewSpan(SpanApiDto raw)
    {
        var start = TimeConversions.NanosToDateTimeOffset(raw.StartTimeUnixNano);
        var durationMs = TimeConversions.NanosToMs(raw.DurationNs);
        var status = raw.StatusCode switch { 0 => "UNSET", 1 => "OK", 2 => "ERROR", _ => "UNSET" };

        return new TraceSpanDto(
            raw.TraceId,
            raw.SpanId,
            raw.ParentSpanId,
            raw.Name ?? "unknown",
            raw.ServiceName ?? "unknown",
            start.ToString("O"),
            durationMs,
            status,
            raw.Attributes,
            raw.Events);
    }
}

#region Payload DTOs

internal sealed record TraceViewerPayload(
    [property: JsonPropertyName("spans")] List<TraceSpanDto> Spans,
    [property: JsonPropertyName("totalDurationMs")]
    double TotalDurationMs);

internal sealed record TraceSpanDto(
    [property: JsonPropertyName("traceId")]
    string TraceId,
    [property: JsonPropertyName("spanId")] string SpanId,
    [property: JsonPropertyName("parentSpanId")]
    string? ParentSpanId,
    [property: JsonPropertyName("operationName")]
    string OperationName,
    [property: JsonPropertyName("serviceName")]
    string ServiceName,
    [property: JsonPropertyName("startTime")]
    string StartTime,
    [property: JsonPropertyName("duration")]
    double Duration,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("attributes")]
    Dictionary<string, object>? Attributes,
    [property: JsonPropertyName("events")] List<SpanEventDto>? Events);

#endregion

#region API DTOs

internal sealed record SpanSearchApiResponse(
    [property: JsonPropertyName("items")] List<SpanApiDto>? Items,
    [property: JsonPropertyName("spans")] List<SpanApiDto>? Spans,
    [property: JsonPropertyName("total")] int Total);

internal sealed record SpanApiDto(
    [property: JsonPropertyName("trace_id")]
    string TraceId,
    [property: JsonPropertyName("span_id")]
    string SpanId,
    [property: JsonPropertyName("parent_span_id")]
    string? ParentSpanId,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("service_name")]
    string? ServiceName,
    [property: JsonPropertyName("start_time_unix_nano")]
    long StartTimeUnixNano,
    [property: JsonPropertyName("duration_ns")]
    long DurationNs,
    [property: JsonPropertyName("status_code")]
    int StatusCode,
    [property: JsonPropertyName("status_message")]
    string? StatusMessage,
    [property: JsonPropertyName("attributes")]
    Dictionary<string, object>? Attributes,
    [property: JsonPropertyName("events")] List<SpanEventDto>? Events);

internal sealed record SpanEventDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("timestamp")]
    string? Timestamp,
    [property: JsonPropertyName("attributes")]
    Dictionary<string, object>? Attributes);

#endregion

[JsonSerializable(typeof(TraceViewerPayload))]
[JsonSerializable(typeof(SpanSearchApiResponse))]
[JsonSerializable(typeof(List<TraceSpanDto>))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class TraceExplorerJsonContext : JsonSerializerContext;
