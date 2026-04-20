namespace qyl.mcp.Tools.Traces;

using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using Formatting;
using mcp.Errors;
using ModelContextProtocol.Server;

/// <summary>
///     Retrieves the full span tree for a trace, returning all spans with timing, status, and attributes.
/// </summary>
/// <param name="client">The HTTP client for backend API communication.</param>
[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed class GetTraceDetailsTool(HttpClient client)
{
    [QylCapability("trace_investigation")]
    [QylCapability("log_investigation", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "get_trace_details", Title = "Get Trace Details",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Get full span tree for a trace. Returns all spans with timing, status, and attributes.")]
    public async Task<string> GetTraceDetailsAsync(
        [Description("The trace ID to inspect")]
        string traceId,
        CancellationToken ct = default)
    {
        var response = await client.GetAsync(
            $"/api/v1/mcp/traces/{Uri.EscapeDataString(traceId)}", ct).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound)
            throw new QylNotFoundException("Trace");

        response.EnsureSuccessStatusCode();

        var spans = await response.Content
            .ReadFromJsonAsync<IReadOnlyList<SpanDetailDto>>(ct).ConfigureAwait(false);

        if (spans is null or { Count: 0 })
            throw new QylNotFoundException("Trace");

        var sb = new StringBuilder();
        sb.AppendLine($"# Trace `{traceId}` ({spans.Count} spans)");
        sb.AppendLine();

        foreach (var span in spans)
        {
            var parentInfo = span.ParentSpanId is not null ? $" (parent: `{span.ParentSpanId}`)" : " (root)";
            sb.AppendLine($"## `{span.SpanId}` {span.SpanName}{parentInfo}");
            sb.AppendLine(ResponseFormatter.FormatDetail(
                span.SpanName,
                [
                    ("Service", span.ServiceName),
                    ("Status", span.Status),
                    ("Start", span.StartTime),
                    ("End", span.EndTime),
                    ("Duration", $"{span.DurationMs:F1}ms")
                ]));

            if (span.Attributes is { Count: > 0 })
            {
                sb.AppendLine("**Attributes:**");
                foreach (var (key, value) in span.Attributes)
                    sb.AppendLine($"  - `{key}`: {value}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}
