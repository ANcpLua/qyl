using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using Qyl.Common;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tools for querying spans.
/// </summary>
[McpServerToolType]
public sealed class SpanQueryTools(HttpClient client)
{
    [McpServerTool(Name = "qyl.search_spans", Title = "Search Spans",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("""
                 Search spans with flexible filtering.

                 This is the general-purpose span query tool. For GenAI-specific
                 queries, use list_genai_spans instead.

                 Supports filtering by:
                 - Session ID
                 - Service name
                 - Operation name (partial match)
                 - Status (ok/error)
                 - Time range

                 Example queries:
                 - Errors only: search_spans(status="error")
                 - By service: search_spans(service_name="api-gateway")
                 - By operation: search_spans(operation="HTTP GET")

                 Returns: List of matching spans with timing and attributes
                 """)]
    public Task<string> SearchSpansAsync(
        [Description("Filter by session ID")] string? sessionId = null,
        [Description("Filter by service name")]
        string? serviceName = null,
        [Description("Filter by operation name (partial match)")]
        string? operation = null,
        [Description("Filter by status: 'ok' or 'error'")]
        string? status = null,
        [Description("Time window in hours (default: 24)")]
        int hours = 24,
        [Description("Maximum spans to return (default: 100)")]
        int limit = 100) => CollectorHelper.ExecuteAsync(async () =>
    {
        var url = !string.IsNullOrEmpty(sessionId)
            ? $"/api/v1/sessions/{Uri.EscapeDataString(sessionId)}/spans"
            : "/api/v1/genai/spans";

        var queryParams = new List<string> { $"limit={limit}", $"hours={hours}" };
        if (!string.IsNullOrEmpty(status))
            queryParams.Add($"status={Uri.EscapeDataString(status)}");

        if (queryParams.Count > 0)
            url += "?" + string.Join("&", queryParams);

        var response = await client.GetFromJsonAsync<SpanSearchResponse>(
            url, SpanQueryJsonContext.Default.SpanSearchResponse).ConfigureAwait(false);

        var spans = response?.Items ?? response?.Spans;

        if (spans is null || spans.Count is 0)
            return "No spans found matching the criteria.";

        // Apply client-side filters
        if (!string.IsNullOrEmpty(serviceName))
        {
            spans =
            [
                .. spans.Where(s =>
                    s.ServiceName?.ContainsIgnoreCase(serviceName) is true)
            ];
        }

        if (!string.IsNullOrEmpty(operation))
        {
            spans =
            [
                .. spans.Where(s =>
                    s.Name?.ContainsIgnoreCase(operation) is true)
            ];
        }

        if (spans.Count is 0)
            return "No spans found matching the criteria after filtering.";

        var sb = new StringBuilder();
        sb.AppendLine($"# Span Search Results ({spans.Count} spans)");
        sb.AppendLine();

        foreach (var span in spans.Take(limit))
        {
            var durationMs = TimeConversions.NanosToMs(span.DurationNs);
            var statusIcon = span.StatusCode == 2 ? "[ERROR]" : "[OK]";
            var timestamp = TimeConversions.NanosToDateTimeOffset((long)span.StartTimeUnixNano);

            sb.AppendLine($"**{timestamp:HH:mm:ss}** {span.Name} {statusIcon} ({durationMs:F0}ms)");

            if (!string.IsNullOrEmpty(span.ServiceName))
                sb.AppendLine($"  Service: {span.ServiceName}");

            if (span.StatusCode == 2 && !string.IsNullOrEmpty(span.StatusMessage))
                sb.AppendLine($"  Error: {span.StatusMessage}");

            sb.AppendLine($"  Trace: {span.TraceId}");
            sb.AppendLine();
        }

        return sb.ToString();
    });
}

#region DTOs

internal sealed record SpanSearchResponse(
    [property: JsonPropertyName("items")] List<SpanSearchDto>? Items,
    [property: JsonPropertyName("spans")] List<SpanSearchDto>? Spans,
    [property: JsonPropertyName("total")] int Total);

internal sealed record SpanSearchDto(
    [property: JsonPropertyName("trace_id")]
    string TraceId,
    [property: JsonPropertyName("span_id")]
    string SpanId,
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
    string? StatusMessage);

#endregion

[JsonSerializable(typeof(SpanSearchResponse))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal sealed partial class SpanQueryJsonContext : JsonSerializerContext;
