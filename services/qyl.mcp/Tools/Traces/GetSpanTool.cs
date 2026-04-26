using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using ModelContextProtocol.Server;
using qyl.mcp.Errors;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Traces;

/// <summary>
///     Retrieves full details for a single span including all attributes.
/// </summary>
/// <param name="client">The HTTP client for backend API communication.</param>
[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed class GetSpanTool(HttpClient client)
{
    [QylCapability("trace_investigation", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "get_span", Title = "Get Span",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Get full details for a single span including all attributes.")]
    public async Task<string> GetSpanAsync(
        [Description("The span ID to inspect")]
        string spanId,
        CancellationToken ct = default)
    {
        var response = await client.GetAsync(
            $"/api/v1/mcp/spans/{Uri.EscapeDataString(spanId)}", ct).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound)
            throw new QylNotFoundException("Span");

        response.EnsureSuccessStatusCode();

        var span = await response.Content
            .ReadFromJsonAsync<SpanDetailDto>(ct).ConfigureAwait(false);

        if (span is null)
            throw new QylNotFoundException("Span");

        var fields = new List<(string Label, string? Value)>
        {
            ("Span ID", $"`{span.SpanId}`"),
            ("Trace ID", $"`{span.TraceId}`"),
            ("Parent Span ID", span.ParentSpanId is not null ? $"`{span.ParentSpanId}`" : null),
            ("Name", span.SpanName),
            ("Service", span.ServiceName),
            ("Status", span.Status),
            ("Start", span.StartTime),
            ("End", span.EndTime),
            ("Duration", $"{span.DurationMs:F1}ms")
        };

        var result = ResponseFormatter.FormatDetail($"Span: {span.SpanName}", fields);

        if (span.Attributes is { Count: > 0 })
        {
            result += "\n**Attributes:**\n";
            foreach (var (key, value) in span.Attributes)
                result += $"  - `{key}`: {value}\n";
        }

        return result;
    }
}
