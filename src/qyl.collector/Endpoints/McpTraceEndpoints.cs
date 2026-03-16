using Microsoft.AspNetCore.Mvc;

namespace Qyl.Collector.Endpoints;

/// <summary>
///     MCP trace endpoints — /api/v1/mcp/traces, /api/v1/mcp/spans.
///     Backs the trace-related MCP tools with cursor-paginated queries.
/// </summary>
internal static class McpTraceEndpoints
{
    public static WebApplication MapMcpTraceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/mcp");

        group.MapGet("/traces", SearchTracesAsync);
        group.MapGet("/spans/{spanId}", GetSpanDetailAsync);
        group.MapPost("/traces/{traceId}/annotations", AnnotateTraceAsync);
        group.MapPost("/traces/{traceId}/reviewed", MarkTraceReviewedAsync);

        return app;
    }

    /// <summary>
    ///     GET /api/v1/mcp/traces?q=...&amp;project=...&amp;cursor=...&amp;limit=...
    ///     Cursor-paginated trace search. Cursor is the start_time_unix_nano of the last item.
    /// </summary>
    private static async Task<IResult> SearchTracesAsync(
        [FromServices] DuckDbStore store,
        [FromQuery] string? q,
        [FromQuery] string? project,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken ct)
    {
        var boundedLimit = Math.Clamp(limit ?? 50, 1, 200);
        var fetchLimit = boundedLimit + 1; // fetch one extra to detect next page

        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();

        var qb = new McpQueryBuilder();

        if (!string.IsNullOrWhiteSpace(q))
            qb.Add("(name ILIKE $N OR status_message ILIKE $N OR attributes_json ILIKE $N)", $"%{q}%");
        if (!string.IsNullOrWhiteSpace(project))
            qb.Add("service_name = $N", project);
        if (!string.IsNullOrWhiteSpace(cursor) && ulong.TryParse(cursor, out var cursorNano))
            qb.Add("start_time_unix_nano < $N", (decimal)cursorNano);

        cmd.CommandText = $"""
                           SELECT trace_id, name, service_name, status_code,
                                  start_time_unix_nano, end_time_unix_nano, duration_ns,
                                  COUNT(*) OVER (PARTITION BY trace_id) as span_count
                           FROM spans
                           {qb.WhereClause}
                           ORDER BY start_time_unix_nano DESC
                           LIMIT {qb.NextParam}
                           """;
        qb.ApplyTo(cmd);
        cmd.Parameters.Add(new DuckDBParameter { Value = fetchLimit });

        var items = new List<McpTraceSummaryDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            items.Add(new McpTraceSummaryDto
            {
                TraceId = reader.GetString(0),
                Name = reader.Col(1).AsString,
                ServiceName = reader.Col(2).AsString,
                StatusCode = reader.Col(3).GetByte(0),
                StartTimeUnixNano = reader.Col(4).GetUInt64(0),
                EndTimeUnixNano = reader.Col(5).GetUInt64(0),
                DurationNs = reader.Col(6).GetUInt64(0),
                SpanCount = reader.Col(7).GetInt64(0)
            });
        }

        var hasMore = items.Count > boundedLimit;
        if (hasMore)
            items.RemoveAt(items.Count - 1);

        var nextCursor = hasMore && items.Count > 0
            ? items[^1].StartTimeUnixNano.ToString()
            : null;

        return TypedResults.Ok(new McpPagedResult<McpTraceSummaryDto>
        {
            Items = items, NextCursor = nextCursor, HasMore = hasMore
        });
    }

    /// <summary>
    ///     GET /api/v1/mcp/spans/{spanId} — full span detail by span ID.
    /// </summary>
    private static async Task<IResult> GetSpanDetailAsync(
        string spanId,
        [FromServices] DuckDbStore store,
        CancellationToken ct)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();

        cmd.CommandText = """
                          SELECT span_id, trace_id, parent_span_id, session_id,
                                 name, kind, start_time_unix_nano, end_time_unix_nano, duration_ns,
                                 status_code, status_message, service_name,
                                 gen_ai_provider_name, gen_ai_request_model, gen_ai_response_model,
                                 gen_ai_input_tokens, gen_ai_output_tokens, gen_ai_temperature,
                                 gen_ai_stop_reason, gen_ai_tool_name, gen_ai_tool_call_id,
                                 gen_ai_cost_usd, attributes_json, resource_json
                          FROM spans
                          WHERE span_id = $1
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = spanId });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return TypedResults.NotFound();

        var detail = new McpSpanDetailDto
        {
            SpanId = reader.GetString(0),
            TraceId = reader.GetString(1),
            ParentSpanId = reader.Col(2).AsString,
            SessionId = reader.Col(3).AsString,
            Name = reader.Col(4).AsString,
            Kind = reader.Col(5).GetByte(0),
            StartTimeUnixNano = reader.Col(6).GetUInt64(0),
            EndTimeUnixNano = reader.Col(7).GetUInt64(0),
            DurationNs = reader.Col(8).GetUInt64(0),
            StatusCode = reader.Col(9).GetByte(0),
            StatusMessage = reader.Col(10).AsString,
            ServiceName = reader.Col(11).AsString,
            GenAiProviderName = reader.Col(12).AsString,
            GenAiRequestModel = reader.Col(13).AsString,
            GenAiResponseModel = reader.Col(14).AsString,
            GenAiInputTokens = reader.Col(15).AsInt64,
            GenAiOutputTokens = reader.Col(16).AsInt64,
            GenAiTemperature = reader.Col(17).AsDouble,
            GenAiStopReason = reader.Col(18).AsString,
            GenAiToolName = reader.Col(19).AsString,
            GenAiToolCallId = reader.Col(20).AsString,
            GenAiCostUsd = reader.Col(21).AsDouble,
            AttributesJson = reader.Col(22).AsString,
            ResourceJson = reader.Col(23).AsString
        };

        return TypedResults.Ok(detail);
    }

    /// <summary>
    ///     POST /api/v1/mcp/traces/{traceId}/annotations — attach a note and tags to a trace.
    ///     Stores annotations in the span_annotations table (created if needed).
    /// </summary>
    private static async Task<IResult> AnnotateTraceAsync(
        string traceId,
        [FromBody] McpAnnotationRequest request,
        [FromServices] DuckDbStore store,
        CancellationToken ct)
    {
        var annotationId = Guid.NewGuid().ToString("N");
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;

        await store.ExecuteWriteAsync(async (con, token) =>
        {
            // Ensure annotation table exists
            await using var ddlCmd = con.CreateCommand();
            ddlCmd.CommandText = """
                                 CREATE TABLE IF NOT EXISTS span_annotations (
                                     id VARCHAR NOT NULL PRIMARY KEY,
                                     trace_id VARCHAR NOT NULL,
                                     note VARCHAR,
                                     tags VARCHAR,
                                     created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
                                 )
                                 """;
            await ddlCmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);

            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO span_annotations (id, trace_id, note, tags, created_at)
                              VALUES ($1, $2, $3, $4, $5)
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = annotationId });
            cmd.Parameters.Add(new DuckDBParameter { Value = traceId });
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
            TraceId = traceId,
            Note = request.Note,
            Tags = request.Tags,
            CreatedAt = now
        });
    }

    /// <summary>
    ///     POST /api/v1/mcp/traces/{traceId}/reviewed — mark a trace as reviewed.
    /// </summary>
    private static async Task<IResult> MarkTraceReviewedAsync(
        string traceId,
        [FromServices] DuckDbStore store,
        CancellationToken ct)
    {
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;

        await store.ExecuteWriteAsync(async (con, token) =>
        {
            // Ensure reviews table exists
            await using var ddlCmd = con.CreateCommand();
            ddlCmd.CommandText = """
                                 CREATE TABLE IF NOT EXISTS trace_reviews (
                                     trace_id VARCHAR NOT NULL PRIMARY KEY,
                                     reviewed_at TIMESTAMP NOT NULL
                                 )
                                 """;
            await ddlCmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);

            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO trace_reviews (trace_id, reviewed_at)
                              VALUES ($1, $2)
                              ON CONFLICT (trace_id) DO UPDATE SET reviewed_at = EXCLUDED.reviewed_at
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = traceId });
            cmd.Parameters.Add(new DuckDBParameter { Value = now });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        return TypedResults.Ok(new McpReviewResponseDto { TraceId = traceId, ReviewedAt = now });
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// DTOs — Trace Endpoints
// ═══════════════════════════════════════════════════════════════════════════════

internal sealed record McpPagedResult<T>
{
    [JsonPropertyName("items")] public required IReadOnlyList<T> Items { get; init; }
    [JsonPropertyName("next_cursor")] public string? NextCursor { get; init; }
    [JsonPropertyName("has_more")] public required bool HasMore { get; init; }
}

internal sealed record McpTraceSummaryDto
{
    [JsonPropertyName("trace_id")] public required string TraceId { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("service_name")] public string? ServiceName { get; init; }
    [JsonPropertyName("status_code")] public byte StatusCode { get; init; }

    [JsonPropertyName("start_time_unix_nano")]
    public ulong StartTimeUnixNano { get; init; }

    [JsonPropertyName("end_time_unix_nano")]
    public ulong EndTimeUnixNano { get; init; }

    [JsonPropertyName("duration_ns")] public ulong DurationNs { get; init; }
    [JsonPropertyName("span_count")] public long SpanCount { get; init; }
}

internal sealed record McpSpanDetailDto
{
    [JsonPropertyName("span_id")] public required string SpanId { get; init; }
    [JsonPropertyName("trace_id")] public required string TraceId { get; init; }
    [JsonPropertyName("parent_span_id")] public string? ParentSpanId { get; init; }
    [JsonPropertyName("session_id")] public string? SessionId { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("kind")] public byte Kind { get; init; }

    [JsonPropertyName("start_time_unix_nano")]
    public ulong StartTimeUnixNano { get; init; }

    [JsonPropertyName("end_time_unix_nano")]
    public ulong EndTimeUnixNano { get; init; }

    [JsonPropertyName("duration_ns")] public ulong DurationNs { get; init; }
    [JsonPropertyName("status_code")] public byte StatusCode { get; init; }
    [JsonPropertyName("status_message")] public string? StatusMessage { get; init; }
    [JsonPropertyName("service_name")] public string? ServiceName { get; init; }

    [JsonPropertyName("gen_ai_provider_name")]
    public string? GenAiProviderName { get; init; }

    [JsonPropertyName("gen_ai_request_model")]
    public string? GenAiRequestModel { get; init; }

    [JsonPropertyName("gen_ai_response_model")]
    public string? GenAiResponseModel { get; init; }

    [JsonPropertyName("gen_ai_input_tokens")]
    public long? GenAiInputTokens { get; init; }

    [JsonPropertyName("gen_ai_output_tokens")]
    public long? GenAiOutputTokens { get; init; }

    [JsonPropertyName("gen_ai_temperature")]
    public double? GenAiTemperature { get; init; }

    [JsonPropertyName("gen_ai_stop_reason")]
    public string? GenAiStopReason { get; init; }

    [JsonPropertyName("gen_ai_tool_name")] public string? GenAiToolName { get; init; }

    [JsonPropertyName("gen_ai_tool_call_id")]
    public string? GenAiToolCallId { get; init; }

    [JsonPropertyName("gen_ai_cost_usd")] public double? GenAiCostUsd { get; init; }
    [JsonPropertyName("attributes_json")] public string? AttributesJson { get; init; }
    [JsonPropertyName("resource_json")] public string? ResourceJson { get; init; }
}

internal sealed record McpAnnotationRequest
{
    [JsonPropertyName("note")] public string? Note { get; init; }
    [JsonPropertyName("tags")] public List<string>? Tags { get; init; }
}

internal sealed record McpAnnotationResponseDto
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("trace_id")] public required string TraceId { get; init; }
    [JsonPropertyName("note")] public string? Note { get; init; }
    [JsonPropertyName("tags")] public List<string>? Tags { get; init; }
    [JsonPropertyName("created_at")] public DateTime CreatedAt { get; init; }
}

internal sealed record McpReviewResponseDto
{
    [JsonPropertyName("trace_id")] public required string TraceId { get; init; }
    [JsonPropertyName("reviewed_at")] public DateTime ReviewedAt { get; init; }
}
