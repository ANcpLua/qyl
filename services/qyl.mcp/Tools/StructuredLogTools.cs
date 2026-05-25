using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ANcpLua.Roslyn.Utilities.Web;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Qyl.Contracts.Primitives;

namespace qyl.mcp.Tools;

[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed partial class StructuredLogTools(HttpClient client)
{
    [McpServerTool(Name = "qyl.list_structured_logs", Title = "List Structured Logs",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true,
        TaskSupport = ToolTaskSupport.Optional)]
    public partial Task<string> ListStructuredLogsAsync(
        string? sessionId = null,
        string? traceId = null,
        string? level = null,
        int? minSeverity = null,
        string? search = null,
        int limit = 100) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var url = QueryString.AppendPairs(
                $"/api/v1/logs?limit={limit}",
                ("session", sessionId), ("trace", traceId), ("level", level),
                ("minSeverity", minSeverity?.ToString(CultureInfo.InvariantCulture)),
                ("search", search));

            var response = await client.GetFromJsonAsync<LogsResponse>(
                url, LogsJsonContext.Default.LogsResponse).ConfigureAwait(false);

            if (response?.Logs is null || response.Logs.Count is 0)
                return "No structured logs found matching the criteria.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Structured Logs ({response.Logs.Count} entries)");
            if (response.HasMore)
                sb.AppendLine("_(More logs available, increase limit or narrow filters)_");
            sb.AppendLine();

            foreach (var log in response.Logs)
            {
                var severity = FormatSeverity(log.SeverityNumber);
                var timestamp = FormatTimestamp(log.TimestampUnixNano);

                sb.AppendLine($"**{timestamp}** [{severity}] {log.Body}");

                if (!string.IsNullOrEmpty(log.ServiceName))
                    sb.AppendLine($"  Service: {log.ServiceName}");

                if (!string.IsNullOrEmpty(log.TraceId))
                    sb.AppendLine($"  Trace: {log.TraceId}");

                if (!string.IsNullOrEmpty(log.SpanId))
                    sb.AppendLine($"  Span: {log.SpanId}");

                if (!string.IsNullOrEmpty(log.SourceFile) || log.SourceLine.HasValue ||
                    !string.IsNullOrEmpty(log.SourceMethod))
                {
                    var location = log.SourceLine.HasValue
                        ? $"{log.SourceFile}:{log.SourceLine}"
                        : log.SourceFile ?? "(unknown)";
                    var methodName = string.IsNullOrWhiteSpace(log.SourceMethod) ? "unknown_method" : log.SourceMethod;
                    sb.AppendLine($"  at {methodName} in {location}");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        });

    [QylCapability("trace_investigation", QylCapabilityRole.FollowUp)]
    [QylCapability("log_investigation")]
    [McpServerTool(Name = "qyl.list_trace_logs", Title = "List Trace Logs",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    public partial Task<string> ListTraceLogsAsync(
        string traceId)
    {
        if (string.IsNullOrEmpty(traceId))
            return Task.FromResult("Error: trace_id is required");

        return CollectorHelper.ExecuteAsync(async () =>
        {
            var url = $"/api/v1/logs?trace={Uri.EscapeDataString(traceId)}&limit=500";

            var response = await client.GetFromJsonAsync<LogsResponse>(
                url, LogsJsonContext.Default.LogsResponse).ConfigureAwait(false);

            if (response?.Logs is null || response.Logs.Count is 0)
                return $"No logs found for trace '{traceId}'.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Logs for Trace: {traceId}");
            sb.AppendLine($"Total: {response.Logs.Count} log entries");
            sb.AppendLine();

            var bySpan = response.Logs.GroupBy(static l => l.SpanId ?? "(no span)").ToList();

            foreach (var group in bySpan)
            {
                sb.AppendLine($"## Span: {group.Key}");
                sb.AppendLine();

                foreach (var log in group.OrderBy(static l => l.TimestampUnixNano))
                {
                    var severity = FormatSeverity(log.SeverityNumber);
                    var timestamp = FormatTimestamp(log.TimestampUnixNano);

                    sb.AppendLine($"  {timestamp} [{severity}] {log.Body}");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        });
    }

    [QylCapability("log_investigation", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "qyl.search_logs", Title = "Search Logs",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true,
        TaskSupport = ToolTaskSupport.Optional)]
    public partial Task<string> SearchLogsAsync(
        string query,
        int? minSeverity = null,
        int hours = 24,
        int limit = 50)
    {
        if (string.IsNullOrEmpty(query))
            return Task.FromResult("Error: search query is required");

        return CollectorHelper.ExecuteAsync(async () =>
        {
            var url = $"/api/v1/logs?search={Uri.EscapeDataString(query)}&limit={limit}";
            if (minSeverity.HasValue)
                url += $"&minSeverity={minSeverity.Value}";

            var response = await client.GetFromJsonAsync<LogsResponse>(
                url, LogsJsonContext.Default.LogsResponse).ConfigureAwait(false);

            if (response?.Logs is null || response.Logs.Count is 0)
                return $"No logs matching '{query}' in the last {hours} hours.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Log Search Results for '{query}'");
            sb.AppendLine($"Found {response.Logs.Count} matching log(s)");
            sb.AppendLine();

            foreach (var log in response.Logs)
            {
                var severity = FormatSeverity(log.SeverityNumber);
                var timestamp = FormatTimestamp(log.TimestampUnixNano);

                sb.AppendLine($"## {timestamp} [{severity}]");
                sb.AppendLine();
                sb.AppendLine(log.Body);

                if (!string.IsNullOrEmpty(log.ServiceName))
                    sb.AppendLine($"\n**Service:** {log.ServiceName}");

                if (!string.IsNullOrEmpty(log.TraceId))
                    sb.AppendLine($"**Trace:** {log.TraceId}");

                sb.AppendLine();
            }

            return sb.ToString();
        });
    }

    private static string FormatSeverity(int? severity) =>
        severity switch
        {
            >= 21 => "FATAL",
            >= 17 => "ERROR",
            >= 13 => "WARN",
            >= 9 => "INFO",
            >= 5 => "DEBUG",
            >= 1 => "TRACE",
            _ => "UNSET"
        };

    private static string FormatTimestamp(long? unixNano)
    {
        if (unixNano is null or 0) return "??:??:??";
        var dt = TimeConversions.NanosToDateTimeOffset(unixNano.Value);
        return dt.ToString("HH:mm:ss.fff");
    }
}

#region DTOs

internal sealed record LogsResponse(
    [property: JsonPropertyName("logs")] List<LogRecordDto>? Logs,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("has_more")]
    bool HasMore);

internal sealed record LogRecordDto(
    [property: JsonPropertyName("timeUnixNano")]
    long? TimestampUnixNano,
    [property: JsonPropertyName("severityNumber")]
    int? SeverityNumber,
    [property: JsonPropertyName("severityText")]
    string? SeverityText,
    [property: JsonPropertyName("body")] string? Body,
    [property: JsonPropertyName("traceId")]
    string? TraceId,
    [property: JsonPropertyName("spanId")] string? SpanId,
    [property: JsonPropertyName("sessionId")]
    string? SessionId,
    [property: JsonPropertyName("serviceName")]
    string? ServiceName,
    [property: JsonPropertyName("sourceFile")]
    string? SourceFile,
    [property: JsonPropertyName("sourceLine")]
    int? SourceLine,
    [property: JsonPropertyName("sourceColumn")]
    int? SourceColumn,
    [property: JsonPropertyName("sourceMethod")]
    string? SourceMethod);

#endregion

[JsonSerializable(typeof(LogsResponse))]
[JsonSerializable(typeof(LogRecordDto))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal sealed partial class LogsJsonContext : JsonSerializerContext;
