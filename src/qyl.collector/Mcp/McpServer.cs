using System.Text.Json;
using System.Text.Json.Serialization;
using qyl.collector.ConsoleBridge;
using qyl.collector.Storage;

namespace qyl.collector.Mcp;

// MCP JSON-RPC protocol requires dynamic payloads - AOT can't statically analyze tool arguments/responses
#pragma warning disable IL2026, IL3050

public sealed class McpServer
{
    private readonly FrontendConsole _console;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly DuckDbStore _store;

    public McpServer(DuckDbStore store, FrontendConsole console)
    {
        _store = store;
        _console = console;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };
    }

    public async Task<McpResponse> HandleToolCallAsync(McpToolCall call, CancellationToken ct = default) =>
        call.Name switch
        {
            "get_sessions" => await GetSessionsAsync(call.Arguments, ct),
            "get_trace" => await GetTraceAsync(call.Arguments, ct),
            "get_spans" => await GetSpansAsync(call.Arguments, ct),
            "get_genai_stats" => await GetGenAiStatsAsync(call.Arguments, ct),
            "search_errors" => await SearchErrorsAsync(call.Arguments, ct),
            "get_storage_stats" => await GetStorageStatsAsync(ct),
            "archive_old_data" => await ArchiveOldDataAsync(call.Arguments, ct),

            "get_console_logs" => GetConsoleLogs(call.Arguments),
            "get_console_errors" => GetConsoleErrors(call.Arguments),
            _ => new McpResponse { Error = $"Unknown tool: {call.Name}" }
        };

    public static McpManifest GetManifest() =>
        new()
        {
            Name = "qyl-telemetry",
            Version = "0.1.0",
            Description = "AI observability and telemetry query server",
            Tools =
            [
                new McpTool
                {
                    Name = "get_sessions",
                    Description = "Get recent sessions with span counts and error rates",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            limit = new
                            {
                                type = "integer", description = "Max sessions to return", @default = 10
                            },
                            service_name = new { type = "string", description = "Filter by service name" }
                        }
                    }
                },
                new McpTool
                {
                    Name = "get_trace",
                    Description = "Get all spans for a specific trace ID",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            trace_id = new { type = "string", description = "The trace ID to fetch" }
                        },
                        required = new[] { "trace_id" }
                    }
                },
                new McpTool
                {
                    Name = "get_spans",
                    Description = "Query spans with filters",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            session_id = new { type = "string", description = "Filter by session ID" },
                            provider_name =
                                new
                                {
                                    type = "string",
                                    description = "Filter by GenAI provider (openai, anthropic, etc.)"
                                },
                            model = new { type = "string", description = "Filter by model name" },
                            status = new { type = "string", description = "Filter by status (ok, error)" },
                            limit = new { type = "integer", description = "Max spans to return", @default = 100 }
                        }
                    }
                },
                new McpTool
                {
                    Name = "get_genai_stats",
                    Description = "Get GenAI usage statistics (tokens, costs, latency)",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            session_id = new { type = "string", description = "Filter by session ID" },
                            hours = new
                            {
                                type = "integer", description = "Time window in hours", @default = 24
                            }
                        }
                    }
                },
                new McpTool
                {
                    Name = "search_errors",
                    Description = "Search for error spans with optional text search",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            query = new
                            {
                                type = "string", description = "Text to search in error messages"
                            },
                            hours = new
                            {
                                type = "integer", description = "Time window in hours", @default = 24
                            },
                            limit = new
                            {
                                type = "integer", description = "Max errors to return", @default = 50
                            }
                        }
                    }
                },
                new McpTool
                {
                    Name = "get_storage_stats",
                    Description = "Get storage statistics (span count, time range, etc.)"
                },
                new McpTool
                {
                    Name = "archive_old_data",
                    Description = "Archive old spans to Parquet files (cold tier)",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            older_than_days = new
                            {
                                type = "integer",
                                description = "Archive spans older than N days",
                                @default = 7
                            }
                        }
                    }
                },

                new McpTool
                {
                    Name = "get_console_logs",
                    Description =
                        "Get frontend console.log messages. Use this to debug client-side JavaScript errors without a browser.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            session = new { type = "string", description = "Filter by session ID" },
                            level = new
                            {
                                type = "string", description = "Min level: debug, log, info, warn, error"
                            },
                            pattern = new
                            {
                                type = "string", description = "Text pattern to search in messages"
                            },
                            limit = new { type = "integer", description = "Max logs to return", @default = 50 }
                        }
                    }
                },
                new McpTool
                {
                    Name = "get_console_errors",
                    Description =
                        "Get frontend console errors and warnings. Quick way to see what's broken in the browser.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            limit = new
                            {
                                type = "integer", description = "Max errors to return", @default = 20
                            }
                        }
                    }
                }
            ]
        };

    private async Task<McpResponse> GetSessionsAsync(JsonElement? args, CancellationToken ct)
    {
        var stats = await _store.GetStorageStatsAsync(ct);
        return new McpResponse
        {
            Content = new McpContent
            {
                Type = "text",
                Text = $"Storage has {stats.SpanCount} spans across {stats.SessionCount} sessions"
            }
        };
    }

    private async Task<McpResponse> GetTraceAsync(JsonElement? args, CancellationToken ct)
    {
        var traceId = args?.GetProperty("trace_id").GetString();
        if (string.IsNullOrEmpty(traceId))
        {
            return new McpResponse { Error = "trace_id is required" };
        }

        var spans = await _store.GetTraceAsync(traceId, ct);
        return new McpResponse
        {
            Content = new McpContent { Type = "text", Text = JsonSerializer.Serialize(spans, _jsonOptions) }
        };
    }

    private async Task<McpResponse> GetSpansAsync(JsonElement? args, CancellationToken ct)
    {
        var sessionId = args?.TryGetProperty("session_id", out var s) == true ? s.GetString() : null;

        if (!string.IsNullOrEmpty(sessionId))
        {
            var spans = await _store.GetSpansBySessionAsync(sessionId, ct);
            return new McpResponse
            {
                Content = new McpContent { Type = "text", Text = JsonSerializer.Serialize(spans, _jsonOptions) }
            };
        }

        return new McpResponse { Error = "session_id filter required for now" };
    }

    private async Task<McpResponse> GetGenAiStatsAsync(JsonElement? args, CancellationToken ct)
    {
        var stats = await _store.GetStorageStatsAsync(ct);

        return new McpResponse
        {
            Content = new McpContent
            {
                Type = "text", Text = $"GenAI stats from {stats.OldestSpan:g} to {stats.NewestSpan:g}"
            }
        };
    }

    private static async Task<McpResponse> SearchErrorsAsync(JsonElement? args, CancellationToken ct)
    {
        await Task.CompletedTask;
        return new McpResponse
        {
            Content = new McpContent { Type = "text", Text = "Error search not yet implemented" }
        };
    }

    private async Task<McpResponse> GetStorageStatsAsync(CancellationToken ct)
    {
        var stats = await _store.GetStorageStatsAsync(ct);
        return new McpResponse
        {
            Content = new McpContent { Type = "text", Text = JsonSerializer.Serialize(stats, _jsonOptions) }
        };
    }

    private async Task<McpResponse> ArchiveOldDataAsync(JsonElement? args, CancellationToken ct)
    {
        var days = args?.TryGetProperty("older_than_days", out var d) == true ? d.GetInt32() : 7;

        var count = await _store.ArchiveToParquetAsync(
            "/data/archive",
            TimeSpan.FromDays(days),
            ct).ConfigureAwait(false);

        return new McpResponse
        {
            Content = new McpContent { Type = "text", Text = $"Archived {count} spans to Parquet files" }
        };
    }

    private McpResponse GetConsoleLogs(JsonElement? args)
    {
        var session = args?.TryGetProperty("session", out var s) == true ? s.GetString() : null;
        var pattern = args?.TryGetProperty("pattern", out var p) == true ? p.GetString() : null;
        var limit = args?.TryGetProperty("limit", out var l) == true ? l.GetInt32() : 50;
        var levelStr = args?.TryGetProperty("level", out var lv) == true ? lv.GetString() : null;

        ConsoleLevel? minLevel = levelStr?.ToLowerInvariant() switch
        {
            "debug" => ConsoleLevel.Debug,
            "info" => ConsoleLevel.Info,
            "warn" => ConsoleLevel.Warn,
            "error" => ConsoleLevel.Error,
            _ => null
        };

        var logs = _console.Query(minLevel, session, pattern, limit);
        var formatted = logs.Select(e => $"[{e.At:HH:mm:ss}] {e.Lvl.ToString().ToUpperInvariant()}: {e.Msg}" +
                                         (e.Url is not null ? $" ({e.Url})" : "") +
                                         (e.Stack is not null ? $"\n  {e.Stack}" : ""));

        return new McpResponse
        {
            Content = new McpContent
            {
                Type = "text",
                Text = logs.Length is 0
                    ? "No console logs found. Is the qyl-console.js shim installed in the frontend?"
                    : string.Join('\n', formatted)
            }
        };
    }

    private McpResponse GetConsoleErrors(JsonElement? args)
    {
        var limit = args?.TryGetProperty("limit", out var l) == true ? l.GetInt32() : 20;
        var errors = _console.Errors(limit);

        var formatted = errors.Select(e => $"[{e.At:HH:mm:ss}] {e.Lvl.ToString().ToUpperInvariant()}: {e.Msg}" +
                                           (e.Url is not null ? $"\n  URL: {e.Url}" : "") +
                                           (e.Stack is not null ? $"\n  Stack: {e.Stack}" : ""));

        return new McpResponse
        {
            Content = new McpContent
            {
                Type = "text",
                Text = errors.Length is 0
                    ? "No console errors found. Either the app is working, or the qyl-console.js shim isn't installed."
                    : $"Found {errors.Length} error(s):\n\n" + string.Join("\n\n", formatted)
            }
        };
    }
}

public sealed class McpManifest
{
    [JsonPropertyName("name")] public required string Name { get; init; }

    [JsonPropertyName("version")] public required string Version { get; init; }

    [JsonPropertyName("description")] public required string Description { get; init; }

    [JsonPropertyName("tools")] public required McpTool[] Tools { get; init; }
}

public sealed class McpTool
{
    [JsonPropertyName("name")] public required string Name { get; init; }

    [JsonPropertyName("description")] public required string Description { get; init; }

    [JsonPropertyName("inputSchema")] public object? InputSchema { get; init; }
}

public sealed class McpToolCall
{
    [JsonPropertyName("name")] public required string Name { get; init; }

    [JsonPropertyName("arguments")] public JsonElement? Arguments { get; init; }
}

public sealed class McpResponse
{
    [JsonPropertyName("content")] public McpContent? Content { get; init; }

    [JsonPropertyName("error")] public string? Error { get; init; }

    [JsonPropertyName("isError")] public bool IsError => Error is not null;
}

public sealed class McpContent
{
    [JsonPropertyName("type")] public required string Type { get; init; }

    [JsonPropertyName("text")] public string? Text { get; init; }
}
