using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ANcpLua.Roslyn.Utilities.Web;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Qyl.Contracts.Primitives;

namespace qyl.mcp.Tools;

[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed partial class SpanQueryTools(HttpClient client)
{
    [QylCapability("trace_investigation", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "qyl.search_spans", Title = "Search Spans",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true,
        TaskSupport = ToolTaskSupport.Optional)]
    public partial Task<string> SearchSpansAsync(
        string? sessionId = null,
        string? serviceName = null,
        string? operation = null,
        string? status = null,
        int hours = 24,
        int limit = 100) => CollectorHelper.ExecuteAsync(async () =>
    {
        var basePath = !string.IsNullOrEmpty(sessionId)
            ? $"/api/v1/sessions/{Uri.EscapeDataString(sessionId)}/spans"
            : "/api/v1/genai/spans";

        var url = QueryString.AppendPairs(
            $"{basePath}?limit={limit}&hours={hours}", ("status", status));

        var response = await client.GetFromJsonAsync<SpanSearchResponse>(
            url, SpanQueryJsonContext.Default.SpanSearchResponse).ConfigureAwait(false);

        var spans = response?.Items ?? response?.Spans;

        if (spans is null || spans.Count is 0)
            return "No spans found matching the criteria.";

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
            var timestamp = TimeConversions.NanosToDateTimeOffset(span.StartTimeUnixNano);

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
