using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using ModelContextProtocol.Server;
using qyl.mcp.Errors;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Logs;

/// <summary>
/// MCP tool that retrieves full details for a single log entry including attributes and correlated trace/span IDs.
/// </summary>
/// <param name="client">The HTTP client used to communicate with the qyl API.</param>
[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed class GetLogDetailsTool(HttpClient client)
{
    /// <summary>
    /// Retrieves all attributes and correlated trace/span IDs for a single log entry.
    /// </summary>
    /// <param name="logId">The log ID to inspect.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A formatted markdown string containing the log entry details and attributes.</returns>
    [QylCapability("log_investigation", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "get_log_details", Title = "Get Log Details",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Get full details for a single log entry including all attributes and correlated trace/span IDs.")]
    public async Task<string> GetLogDetailsAsync(
        [Description("The log ID to inspect")] string logId,
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
