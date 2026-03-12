using Microsoft.AspNetCore.Mvc;

namespace Qyl.Collector.Endpoints;

/// <summary>
///     MCP session endpoints — /api/v1/mcp/sessions.
///     Cursor-paginated session search, detail, annotations, and status management.
/// </summary>
internal static class McpSessionEndpoints
{
    public static WebApplication MapMcpSessionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/mcp");

        group.MapGet("/sessions", SearchSessionsAsync);
        group.MapGet("/sessions/{sessionId}", GetSessionDetailAsync);
        group.MapPost("/sessions/{sessionId}/annotations", AnnotateSessionAsync);
        group.MapMethods("/sessions/{sessionId}/status", ["PATCH"], UpdateSessionStatusAsync);

        return app;
    }

    /// <summary>
    ///     GET /api/v1/mcp/sessions?q=...&amp;cursor=...&amp;limit=...
    ///     Cursor-paginated session search. Cursor is the start_time ISO string.
    /// </summary>
    private static async Task<IResult> SearchSessionsAsync(
        [FromServices] DuckDbStore store,
        [FromQuery] string? q,
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
            qb.Add("(session_id ILIKE $N OR services ILIKE $N)", $"%{q}%");
        if (!string.IsNullOrWhiteSpace(cursor) && DateTime.TryParse(cursor, CultureInfo.InvariantCulture, out var cursorTime))
            qb.Add("start_time < $N", cursorTime);

        cmd.CommandText = $"""
                           SELECT session_id, user_id, start_time, end_time,
                                  duration_ms, trace_count, span_count, error_count,
                                  services, state
                           FROM session_entities
                           {qb.WhereClause}
                           ORDER BY start_time DESC
                           LIMIT {qb.NextParam}
                           """;
        qb.ApplyTo(cmd);
        cmd.Parameters.Add(new DuckDBParameter { Value = fetchLimit });

        var items = new List<McpSessionSummaryDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            items.Add(new McpSessionSummaryDto
            {
                SessionId = reader.GetString(0),
                UserId = reader.Col(1).AsString,
                StartTime = reader.Col(2).AsDateTimeOffset,
                EndTime = reader.Col(3).AsDateTimeOffset,
                DurationMs = reader.Col(4).AsDouble,
                TraceCount = reader.Col(5).GetInt32(0),
                SpanCount = reader.Col(6).GetInt32(0),
                ErrorCount = reader.Col(7).GetInt32(0),
                Services = reader.Col(8).AsString,
                State = reader.Col(9).AsString
            });
        }

        var hasMore = items.Count > boundedLimit;
        if (hasMore)
            items.RemoveAt(items.Count - 1);

        string? nextCursor = hasMore && items.Count > 0
            ? items[^1].StartTime?.ToString("O")
            : null;

        return TypedResults.Ok(new McpPagedResult<McpSessionSummaryDto>
        {
            Items = items,
            NextCursor = nextCursor,
            HasMore = hasMore
        });
    }

    /// <summary>
    ///     GET /api/v1/mcp/sessions/{sessionId} — full session detail.
    /// </summary>
    private static async Task<IResult> GetSessionDetailAsync(
        string sessionId,
        [FromServices] DuckDbStore store,
        CancellationToken ct)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();

        cmd.CommandText = """
                          SELECT session_id, user_id, start_time, end_time,
                                 duration_ms, trace_count, span_count, error_count,
                                 services, state, client, geo, genai_usage
                          FROM session_entities
                          WHERE session_id = $1
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = sessionId });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return TypedResults.NotFound();

        var detail = new McpSessionDetailDto
        {
            SessionId = reader.GetString(0),
            UserId = reader.Col(1).AsString,
            StartTime = reader.Col(2).AsDateTimeOffset,
            EndTime = reader.Col(3).AsDateTimeOffset,
            DurationMs = reader.Col(4).AsDouble,
            TraceCount = reader.Col(5).GetInt32(0),
            SpanCount = reader.Col(6).GetInt32(0),
            ErrorCount = reader.Col(7).GetInt32(0),
            Services = reader.Col(8).AsString,
            State = reader.Col(9).AsString,
            Client = reader.Col(10).AsString,
            Geo = reader.Col(11).AsString,
            GenAiUsage = reader.Col(12).AsString
        };

        return TypedResults.Ok(detail);
    }

    /// <summary>
    ///     POST /api/v1/mcp/sessions/{sessionId}/annotations — attach a note and tags.
    /// </summary>
    private static async Task<IResult> AnnotateSessionAsync(
        string sessionId,
        [FromBody] McpAnnotationRequest request,
        [FromServices] DuckDbStore store,
        CancellationToken ct)
    {
        var annotationId = Guid.NewGuid().ToString("N");
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;

        await store.ExecuteWriteAsync(async (con, token) =>
        {
            await using var ddlCmd = con.CreateCommand();
            ddlCmd.CommandText = """
                                 CREATE TABLE IF NOT EXISTS session_annotations (
                                     id VARCHAR NOT NULL PRIMARY KEY,
                                     session_id VARCHAR NOT NULL,
                                     note VARCHAR,
                                     tags VARCHAR,
                                     created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
                                 )
                                 """;
            await ddlCmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);

            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO session_annotations (id, session_id, note, tags, created_at)
                              VALUES ($1, $2, $3, $4, $5)
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = annotationId });
            cmd.Parameters.Add(new DuckDBParameter { Value = sessionId });
            cmd.Parameters.Add(new DuckDBParameter { Value = request.Note ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter
            {
                Value = request.Tags is { Count: > 0 }
                    ? string.Join(",", request.Tags)
                    : DBNull.Value
            });
            cmd.Parameters.Add(new DuckDBParameter { Value = now });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        return TypedResults.Ok(new McpAnnotationResponseDto
        {
            Id = annotationId,
            TraceId = sessionId,
            Note = request.Note,
            Tags = request.Tags,
            CreatedAt = now
        });
    }

    /// <summary>
    ///     PATCH /api/v1/mcp/sessions/{sessionId}/status — update session state.
    /// </summary>
    private static async Task<IResult> UpdateSessionStatusAsync(
        string sessionId,
        [FromBody] McpSessionStatusRequest request,
        [FromServices] DuckDbStore store,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Status))
            return TypedResults.BadRequest(new { error = "status is required" });

        var affected = await store.ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              UPDATE session_entities
                              SET state = $1
                              WHERE session_id = $2
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = request.Status });
            cmd.Parameters.Add(new DuckDBParameter { Value = sessionId });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        return affected > 0
            ? TypedResults.Ok(new { session_id = sessionId, status = request.Status })
            : TypedResults.NotFound();
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// DTOs — Session Endpoints
// ═══════════════════════════════════════════════════════════════════════════════

internal sealed record McpSessionSummaryDto
{
    [JsonPropertyName("session_id")] public required string SessionId { get; init; }
    [JsonPropertyName("user_id")] public string? UserId { get; init; }
    [JsonPropertyName("start_time")] public DateTimeOffset? StartTime { get; init; }
    [JsonPropertyName("end_time")] public DateTimeOffset? EndTime { get; init; }
    [JsonPropertyName("duration_ms")] public double? DurationMs { get; init; }
    [JsonPropertyName("trace_count")] public int TraceCount { get; init; }
    [JsonPropertyName("span_count")] public int SpanCount { get; init; }
    [JsonPropertyName("error_count")] public int ErrorCount { get; init; }
    [JsonPropertyName("services")] public string? Services { get; init; }
    [JsonPropertyName("state")] public string? State { get; init; }
}

internal sealed record McpSessionDetailDto
{
    [JsonPropertyName("session_id")] public required string SessionId { get; init; }
    [JsonPropertyName("user_id")] public string? UserId { get; init; }
    [JsonPropertyName("start_time")] public DateTimeOffset? StartTime { get; init; }
    [JsonPropertyName("end_time")] public DateTimeOffset? EndTime { get; init; }
    [JsonPropertyName("duration_ms")] public double? DurationMs { get; init; }
    [JsonPropertyName("trace_count")] public int TraceCount { get; init; }
    [JsonPropertyName("span_count")] public int SpanCount { get; init; }
    [JsonPropertyName("error_count")] public int ErrorCount { get; init; }
    [JsonPropertyName("services")] public string? Services { get; init; }
    [JsonPropertyName("state")] public string? State { get; init; }
    [JsonPropertyName("client")] public string? Client { get; init; }
    [JsonPropertyName("geo")] public string? Geo { get; init; }
    [JsonPropertyName("genai_usage")] public string? GenAiUsage { get; init; }
}

internal sealed record McpSessionStatusRequest
{
    [JsonPropertyName("status")] public string? Status { get; init; }
}
