using System.Net;
using System.Net.Http.Json;
using ModelContextProtocol.Server;
using qyl.mcp.Errors;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Logs;

[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed partial class GetLogDetailsTool(HttpClient client)
{
    [QylCapability("log_investigation", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "get_log_details", Title = "Get Log Details",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    public async partial Task<string> GetLogDetailsAsync(
        string logId,
        CancellationToken ct = default)
    {
        var response = await client.GetAsync(
            $"/api/v1/mcp/logs/{Uri.EscapeDataString(logId)}", ct).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound)
            throw new QylNotFoundException("Log");

        response.EnsureSuccessStatusCode();

        var log = await response.Content
            .ReadFromJsonAsync<LogDetailDto>(ct).ConfigureAwait(false);

        if (log is null)
            throw new QylNotFoundException("Log");

        var fields = new List<(string Label, string? Value)>
        {
            ("Log ID", $"`{log.LogId}`"),
            ("Timestamp", log.Timestamp),
            ("Severity", log.Severity),
            ("Body", log.Body),
            ("Service", log.ServiceName),
            ("Trace ID", log.TraceId is not null ? $"`{log.TraceId}`" : null),
            ("Span ID", log.SpanId is not null ? $"`{log.SpanId}`" : null)
        };

        var result = ResponseFormatter.FormatDetail($"Log: {log.Severity} from {log.ServiceName}", fields);

        if (log.Attributes is { Count: > 0 })
        {
            result += "\n**Attributes:**\n";
            foreach (var (key, value) in log.Attributes)
                result += $"  - `{key}`: {value}\n";
        }

        return result;
    }
}
