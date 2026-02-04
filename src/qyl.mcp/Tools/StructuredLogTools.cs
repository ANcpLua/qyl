using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tools for accessing OTLP structured logs stored by qyl.collector.
///     These are OpenTelemetry log records (not frontend console.log).
/// </summary>
[McpServerToolType]
public sealed class StructuredLogTools
{
    private readonly HttpClient _client;

    public StructuredLogTools(HttpClient client) => _client = client;

    [McpServerTool(Name = "qyl.list_structured_logs")]
    [Description("""
                 List OTLP structured log records captured by qyl.

                 These are server-side logs sent via OpenTelemetry, not frontend console.log messages.
                 Use 'qyl.list_console_logs' for browser console messages.

                 Supports filtering by:
                 - Session ID: Filter logs from a specific session
                 - Trace ID: Get logs associated with a distributed trace
                 - Severity: Filter by log level (1=Trace, 5=Debug, 9=Info, 13=Warn, 17=Error, 21=Fatal)
                 - Text search: Search in log body and attributes

                 Example queries:
                 - All recent logs: list_structured_logs()
                 - Errors only: list_structured_logs(min_severity=17)
                 - Search text: list_structured_logs(search="connection failed")
                 - Trace logs: list_structured_logs(trace_id="abc123...")

                 Returns: Formatted list of structured logs with timestamps, severity, and attributes
                 """)]
    public async Task<string> ListStructuredLogsAsync(
        [Description("Filter by session ID")]
        string? sessionId = null,
        [Description("Filter by trace ID (correlates with distributed traces)")]
        string? traceId = null,
        [Description("Filter by severity level name: 'trace', 'debug', 'info', 'warn', 'error', 'fatal'")]
        string? level = null,
        [Description("Minimum severity number (1=Trace, 5=Debug, 9=Info, 13=Warn, 17=Error, 21=Fatal)")]
        int? minSeverity = null,
        [Description("Text to search in log body and attributes (case-insensitive)")]
        string? search = null,
        [Description("Maximum number of logs to return (default: 100)")]
        int limit = 100)
    {
        try
        {
            var url = $"/api/v1/logs?limit={limit}";
            if (!string.IsNullOrEmpty(sessionId))
                url += $"&session={Uri.EscapeDataString(sessionId)}";
            if (!string.IsNullOrEmpty(traceId))
                url += $"&trace={Uri.EscapeDataString(traceId)}";
            if (!string.IsNullOrEmpty(level))
                url += $"&level={Uri.EscapeDataString(level)}";
            if (minSeverity.HasValue)
                url += $"&minSeverity={minSeverity.Value}";
            if (!string.IsNullOrEmpty(search))
                url += $"&search={Uri.EscapeDataString(search)}";

            var response = await _client.GetFromJsonAsync<LogsResponse>(
                url, LogsJsonContext.Default.LogsResponse).ConfigureAwait(false);

            if (response?.Logs is null || response.Logs.Count is 0)
                return "No structured logs found matching the criteria.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Structured Logs ({response.Logs.Count} entries)");
            if (response.HasMore)
                sb.AppendLine($"_(More logs available, increase limit or narrow filters)_");
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

                sb.AppendLine();
            }

            return sb.ToString();
        }
        catch (HttpRequestException ex)
        {
            return $"Error connecting to qyl collector: {ex.Message}";
        }
    }

    [McpServerTool(Name = "qyl.list_trace_logs")]
    [Description("""
                 Get all structured logs associated with a distributed trace.

                 This correlates logs with spans in a trace, useful for debugging
                 issues that span multiple services.

                 Parameters:
                 - trace_id: The trace ID (required)

                 Returns: Logs ordered by timestamp for the specified trace
                 """)]
    public async Task<string> ListTraceLogsAsync(
        [Description("The trace ID to get logs for (required)")]
        string traceId)
    {
        if (string.IsNullOrEmpty(traceId))
            return "Error: trace_id is required";

        try
        {
            var url = $"/api/v1/logs?trace={Uri.EscapeDataString(traceId)}&limit=500";

            var response = await _client.GetFromJsonAsync<LogsResponse>(
                url, LogsJsonContext.Default.LogsResponse).ConfigureAwait(false);

            if (response?.Logs is null || response.Logs.Count is 0)
                return $"No logs found for trace '{traceId}'.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Logs for Trace: {traceId}");
            sb.AppendLine($"Total: {response.Logs.Count} log entries");
            sb.AppendLine();

            // Group by span for better readability
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
        }
        catch (HttpRequestException ex)
        {
            return $"Error connecting to qyl collector: {ex.Message}";
        }
    }

    [McpServerTool(Name = "qyl.search_logs")]
    [Description("""
                 Search structured logs by text pattern.

                 Searches in log body and attributes for the given text.

                 Parameters:
                 - query: Text to search for (required)
                 - min_severity: Minimum severity (17 for errors only)
                 - hours: Time window in hours (default: 24)

                 Returns: Matching logs with context
                 """)]
    public async Task<string> SearchLogsAsync(
        [Description("Text to search for in logs (required)")]
        string query,
        [Description("Minimum severity (9=Info, 13=Warn, 17=Error)")]
        int? minSeverity = null,
        [Description("Time window in hours (default: 24)")]
        int hours = 24,
        [Description("Maximum results (default: 50)")]
        int limit = 50)
    {
        if (string.IsNullOrEmpty(query))
            return "Error: search query is required";

        try
        {
            var url = $"/api/v1/logs?search={Uri.EscapeDataString(query)}&limit={limit}";
            if (minSeverity.HasValue)
                url += $"&minSeverity={minSeverity.Value}";

            var response = await _client.GetFromJsonAsync<LogsResponse>(
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
        }
        catch (HttpRequestException ex)
        {
            return $"Error connecting to qyl collector: {ex.Message}";
        }
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
        var dt = DateTimeOffset.FromUnixTimeMilliseconds(unixNano.Value / 1_000_000);
        return dt.ToString("HH:mm:ss.fff");
    }
}

#region DTOs

internal sealed record LogsResponse(
    [property: JsonPropertyName("logs")] List<LogRecordDto>? Logs,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("has_more")] bool HasMore);

internal sealed record LogRecordDto(
    [property: JsonPropertyName("timestamp_unix_nano")] long? TimestampUnixNano,
    [property: JsonPropertyName("severity_number")] int? SeverityNumber,
    [property: JsonPropertyName("severity_text")] string? SeverityText,
    [property: JsonPropertyName("body")] string? Body,
    [property: JsonPropertyName("trace_id")] string? TraceId,
    [property: JsonPropertyName("span_id")] string? SpanId,
    [property: JsonPropertyName("session_id")] string? SessionId,
    [property: JsonPropertyName("service_name")] string? ServiceName);

#endregion

[JsonSerializable(typeof(LogsResponse))]
[JsonSerializable(typeof(LogRecordDto))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal sealed partial class LogsJsonContext : JsonSerializerContext;
