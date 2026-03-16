using Microsoft.AspNetCore.Mvc;

namespace Qyl.Collector.Endpoints;

/// <summary>
///     MCP log endpoints — /api/v1/mcp/logs.
///     Cursor-paginated log search and detail retrieval for MCP tools.
/// </summary>
internal static class McpLogEndpoints
{
    public static WebApplication MapMcpLogEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/mcp");

        group.MapGet("/logs", SearchLogsAsync);
        group.MapGet("/logs/{logId}", GetLogDetailAsync);

        return app;
    }

    /// <summary>
    ///     GET /api/v1/mcp/logs?q=...&amp;project=...&amp;cursor=...&amp;limit=...
    ///     Cursor-paginated log search. Cursor is the time_unix_nano of the last item.
    /// </summary>
    private static async Task<IResult> SearchLogsAsync(
        [FromServices] DuckDbStore store,
        [FromQuery] string? q,
        [FromQuery] string? project,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken ct)
    {
        var boundedLimit = Math.Clamp(limit ?? 50, 1, 200);
        var fetchLimit = boundedLimit + 1;

        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();

        var qb = new McpQueryBuilder();

        if (!string.IsNullOrWhiteSpace(q))
            qb.Add("(body ILIKE $N OR severity_text ILIKE $N OR attributes_json ILIKE $N)", $"%{q}%");
        if (!string.IsNullOrWhiteSpace(project))
            qb.Add("service_name = $N", project);
        if (!string.IsNullOrWhiteSpace(cursor) && ulong.TryParse(cursor, out var cursorNano))
            qb.Add("time_unix_nano < $N", (decimal)cursorNano);

        cmd.CommandText = $"""
                           SELECT log_id, trace_id, span_id, session_id,
                                  time_unix_nano, severity_number, severity_text,
                                  body, service_name
                           FROM logs
                           {qb.WhereClause}
                           ORDER BY time_unix_nano DESC
                           LIMIT {qb.NextParam}
                           """;
        qb.ApplyTo(cmd);
        cmd.Parameters.Add(new DuckDBParameter { Value = fetchLimit });

        var items = new List<McpLogSummaryDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            items.Add(new McpLogSummaryDto
            {
                LogId = reader.GetString(0),
                TraceId = reader.Col(1).AsString,
                SpanId = reader.Col(2).AsString,
                SessionId = reader.Col(3).AsString,
                TimeUnixNano = reader.Col(4).GetUInt64(0),
                SeverityNumber = reader.Col(5).GetByte(0),
                SeverityText = reader.Col(6).AsString,
                Body = reader.Col(7).AsString,
                ServiceName = reader.Col(8).AsString
            });
        }

        var hasMore = items.Count > boundedLimit;
        if (hasMore)
            items.RemoveAt(items.Count - 1);

        var nextCursor = hasMore && items.Count > 0
            ? items[^1].TimeUnixNano.ToString()
            : null;

        return TypedResults.Ok(new McpPagedResult<McpLogSummaryDto>
        {
            Items = items, NextCursor = nextCursor, HasMore = hasMore
        });
    }

    /// <summary>
    ///     GET /api/v1/mcp/logs/{logId} — full log detail by log ID.
    /// </summary>
    private static async Task<IResult> GetLogDetailAsync(
        string logId,
        [FromServices] DuckDbStore store,
        CancellationToken ct)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();

        cmd.CommandText = """
                          SELECT log_id, trace_id, span_id, session_id,
                                 time_unix_nano, observed_time_unix_nano,
                                 severity_number, severity_text, body,
                                 service_name, attributes_json, resource_json,
                                 source_file, source_line, source_column, source_method
                          FROM logs
                          WHERE log_id = $1
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = logId });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return TypedResults.NotFound();

        var detail = new McpLogDetailDto
        {
            LogId = reader.GetString(0),
            TraceId = reader.Col(1).AsString,
            SpanId = reader.Col(2).AsString,
            SessionId = reader.Col(3).AsString,
            TimeUnixNano = reader.Col(4).GetUInt64(0),
            ObservedTimeUnixNano = reader.Col(5).AsUInt64,
            SeverityNumber = reader.Col(6).GetByte(0),
            SeverityText = reader.Col(7).AsString,
            Body = reader.Col(8).AsString,
            ServiceName = reader.Col(9).AsString,
            AttributesJson = reader.Col(10).AsString,
            ResourceJson = reader.Col(11).AsString,
            SourceFile = reader.Col(12).AsString,
            SourceLine = reader.Col(13).AsInt32,
            SourceColumn = reader.Col(14).AsInt32,
            SourceMethod = reader.Col(15).AsString
        };

        return TypedResults.Ok(detail);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// DTOs — Log Endpoints
// ═══════════════════════════════════════════════════════════════════════════════

internal sealed record McpLogSummaryDto
{
    [JsonPropertyName("log_id")] public required string LogId { get; init; }
    [JsonPropertyName("trace_id")] public string? TraceId { get; init; }
    [JsonPropertyName("span_id")] public string? SpanId { get; init; }
    [JsonPropertyName("session_id")] public string? SessionId { get; init; }
    [JsonPropertyName("time_unix_nano")] public ulong TimeUnixNano { get; init; }
    [JsonPropertyName("severity_number")] public byte SeverityNumber { get; init; }
    [JsonPropertyName("severity_text")] public string? SeverityText { get; init; }
    [JsonPropertyName("body")] public string? Body { get; init; }
    [JsonPropertyName("service_name")] public string? ServiceName { get; init; }
}

internal sealed record McpLogDetailDto
{
    [JsonPropertyName("log_id")] public required string LogId { get; init; }
    [JsonPropertyName("trace_id")] public string? TraceId { get; init; }
    [JsonPropertyName("span_id")] public string? SpanId { get; init; }
    [JsonPropertyName("session_id")] public string? SessionId { get; init; }
    [JsonPropertyName("time_unix_nano")] public ulong TimeUnixNano { get; init; }

    [JsonPropertyName("observed_time_unix_nano")]
    public ulong? ObservedTimeUnixNano { get; init; }

    [JsonPropertyName("severity_number")] public byte SeverityNumber { get; init; }
    [JsonPropertyName("severity_text")] public string? SeverityText { get; init; }
    [JsonPropertyName("body")] public string? Body { get; init; }
    [JsonPropertyName("service_name")] public string? ServiceName { get; init; }
    [JsonPropertyName("attributes_json")] public string? AttributesJson { get; init; }
    [JsonPropertyName("resource_json")] public string? ResourceJson { get; init; }
    [JsonPropertyName("source_file")] public string? SourceFile { get; init; }
    [JsonPropertyName("source_line")] public int? SourceLine { get; init; }
    [JsonPropertyName("source_column")] public int? SourceColumn { get; init; }
    [JsonPropertyName("source_method")] public string? SourceMethod { get; init; }
}
