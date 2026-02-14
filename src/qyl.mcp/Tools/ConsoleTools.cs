using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tools for accessing frontend console logs captured by qyl.collector.
///     These tools help AI agents debug client-side JavaScript issues without a browser.
/// </summary>
[McpServerToolType]
public sealed class ConsoleTools(HttpClient client)
{
    [McpServerTool(Name = "qyl.list_console_logs")]
    [Description("""
                 List frontend console.log messages captured by qyl.

                 Use this to debug client-side JavaScript issues without opening a browser.
                 Messages are captured via the qyl-console.js shim installed in the frontend.

                 Example queries:
                 - All logs: list_console_logs()
                 - Errors only: list_console_logs(level="error")
                 - Search for text: list_console_logs(pattern="failed")
                 - Filter by session: list_console_logs(session_id="abc123")

                 Returns: Formatted list of console messages with timestamps, levels, and optional stack traces
                 """)]
    public async Task<string> ListConsoleLogsAsync(
        [Description("Filter by session ID (e.g., 'session-abc123')")]
        string? sessionId = null,
        [Description("Minimum log level: 'debug', 'log', 'info', 'warn', 'error'")]
        string? level = null,
        [Description("Text pattern to search in messages (case-insensitive)")]
        string? pattern = null,
        [Description("Maximum number of logs to return (default: 50)")]
        int limit = 50)
    {
        try
        {
            var url = $"/api/v1/console?limit={limit}";
            if (!string.IsNullOrEmpty(sessionId))
                url += $"&session={Uri.EscapeDataString(sessionId)}";
            if (!string.IsNullOrEmpty(level))
                url += $"&level={Uri.EscapeDataString(level)}";

            var logs = await client.GetFromJsonAsync<ConsoleLogDto[]>(
                url, ConsoleJsonContext.Default.ConsoleLogDtoArray).ConfigureAwait(false);

            if (logs is null || logs.Length is 0)
                return "No console logs found. The qyl-console.js shim may not be installed in the frontend.";

            // Apply pattern filter client-side if specified
            if (!string.IsNullOrEmpty(pattern))
            {
                logs = [.. logs.Where(l => l.Msg?.ContainsIgnoreCase(pattern) is true)];

                if (logs.Length is 0)
                    return $"No console logs matching pattern '{pattern}'.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"# Console Logs ({logs.Length} entries)");
            sb.AppendLine();

            foreach (var log in logs)
            {
                var levelIcon = log.Lvl switch
                {
                    "Error" => "[ERROR]",
                    "Warn" => "[WARN]",
                    "Info" => "[INFO]",
                    "Debug" => "[DEBUG]",
                    _ => "[LOG]"
                };

                sb.AppendLine($"**{log.At:HH:mm:ss}** {levelIcon} {log.Msg}");

                if (!string.IsNullOrEmpty(log.Url))
                    sb.AppendLine($"  Source: {log.Url}");

                if (!string.IsNullOrEmpty(log.Stack))
                    sb.AppendLine($"  Stack: {log.Stack}");

                sb.AppendLine();
            }

            return sb.ToString();
        }
        catch (HttpRequestException ex)
        {
            return $"Error connecting to qyl collector: {ex.Message}";
        }
    }

    [McpServerTool(Name = "qyl.list_console_errors")]
    [Description("""
                 List frontend console errors and warnings.

                 Quick way to see what's broken in the browser. Returns only warn and error level messages.

                 Returns: Formatted list of errors/warnings with timestamps and stack traces
                 """)]
    public async Task<string> ListConsoleErrorsAsync(
        [Description("Maximum number of errors to return (default: 20)")]
        int limit = 20)
    {
        try
        {
            var errors = await client.GetFromJsonAsync<ConsoleLogDto[]>(
                $"/api/v1/console/errors?limit={limit}",
                ConsoleJsonContext.Default.ConsoleLogDtoArray).ConfigureAwait(false);

            if (errors is null || errors.Length is 0)
                return "No console errors found. Either the app is working, or the qyl-console.js shim isn't installed.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Console Errors ({errors.Length} entries)");
            sb.AppendLine();

            foreach (var error in errors)
            {
                var levelIcon = error.Lvl == "Error" ? "[ERROR]" : "[WARN]";

                sb.AppendLine($"## {error.At:HH:mm:ss} {levelIcon}");
                sb.AppendLine();
                sb.AppendLine(error.Msg);

                if (!string.IsNullOrEmpty(error.Url))
                    sb.AppendLine($"\n**Source:** {error.Url}");

                if (!string.IsNullOrEmpty(error.Stack))
                {
                    sb.AppendLine("\n**Stack trace:**");
                    sb.AppendLine($"```\n{error.Stack}\n```");
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
}

#region DTOs

internal sealed record ConsoleLogDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("lvl")] string Lvl,
    [property: JsonPropertyName("msg")] string? Msg,
    [property: JsonPropertyName("at")] DateTime At,
    [property: JsonPropertyName("session")] string? Session,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("stack")] string? Stack);

#endregion

[JsonSerializable(typeof(ConsoleLogDto))]
[JsonSerializable(typeof(ConsoleLogDto[]))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal sealed partial class ConsoleJsonContext : JsonSerializerContext;
