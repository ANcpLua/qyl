namespace Qyl.Collector.Conversations;

/// <summary>
///     PRD #173: roll spans up by <c>session_id</c> for the dashboard Conversations view.
///     Reads from the <c>qyl_conversations</c> view and surfaces capture-gate state so the UI
///     can render tool args/results conditionally without making policy decisions of its own.
/// </summary>
public sealed record ConversationCaptureFlags(
    bool MessageContent,
    bool RecordInputs,
    bool RecordOutputs);

public sealed record ConversationListItem(
    string SessionId,
    long SpanCount,
    DateTime FirstSeen,
    DateTime LastSeen,
    double DurationMs,
    double TotalCostUsd,
    long? InputTokens,
    long? OutputTokens,
    IReadOnlyList<string> Services,
    IReadOnlyList<string> Models);

public sealed record ConversationSpan(
    string SpanId,
    string TraceId,
    string? ParentSpanId,
    string Name,
    string ServiceName,
    DateTime StartTime,
    double DurationMs,
    string? Provider,
    string? RequestModel,
    string? ResponseModel,
    long? InputTokens,
    long? OutputTokens,
    string? ToolName,
    string? ToolCallId,
    double? CostUsd,
    int StatusCode,
    string? StatusMessage,
    JsonElement? Attributes);

public sealed record ConversationDetail(
    string SessionId,
    long SpanCount,
    DateTime FirstSeen,
    DateTime LastSeen,
    double TotalCostUsd,
    IReadOnlyList<ConversationSpan> Spans,
    ConversationCaptureFlags CaptureFlags);

public sealed record ConversationListResponse(
    IReadOnlyList<ConversationListItem> Items,
    int Total,
    ConversationCaptureFlags CaptureFlags);

internal static class ConversationEndpoints
{
    private static readonly Meter ConversationsMeter = new("qyl.observability.conversations", "1.0.0");

    private static readonly Histogram<long> SpanCountHistogram = ConversationsMeter.CreateHistogram<long>(
        "qyl_observability_conversation_span_count",
        unit: "{span}",
        description: "Number of spans returned in a single conversation thread fetch");

    [QylMapEndpoints]
    public static WebApplication MapConversationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/conversations");

        group.MapGet("", ListConversationsAsync);
        group.MapGet("{sessionId}", GetConversationAsync);

        return app;
    }

    private static async Task<IResult> ListConversationsAsync(
        DuckDbStore store,
        IConfiguration configuration,
        int? limit,
        int? hours,
        string? agent,
        CancellationToken ct)
    {
        var boundedLimit = Math.Clamp(limit ?? 100, 1, 500);
        var window = hours is > 0 ? hours.Value : 168;

        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();

        // Optional agent filter — keep the named-session join cheap by gating on the
        // gen_ai.agent.name attribute via DuckDB's JSON probe before grouping.
        var agentFilter = string.IsNullOrWhiteSpace(agent)
            ? string.Empty
            : " AND session_id IN (SELECT session_id FROM qyl_conversations"
              + " WHERE json_extract_string(attributes_json, '$.\"gen_ai.agent.name\"') = $"
              + cmd.AddParam(agent) + ")";

        cmd.CommandText = $"""
                           SELECT
                               session_id,
                               COUNT(*) AS span_count,
                               MIN(start_time_unix_nano) AS first_seen_ns,
                               MAX(end_time_unix_nano) AS last_seen_ns,
                               COALESCE(SUM(gen_ai_cost_usd), 0) AS total_cost_usd,
                               COALESCE(SUM(gen_ai_input_tokens), 0) AS input_tokens,
                               COALESCE(SUM(gen_ai_output_tokens), 0) AS output_tokens,
                               list_distinct(list(service_name)) AS services,
                               list_distinct(list(gen_ai_request_model) FILTER (WHERE gen_ai_request_model IS NOT NULL)) AS models
                           FROM qyl_conversations
                           WHERE start_time_unix_nano > (CAST(epoch_ns(now()) AS UBIGINT) - CAST({window.ToString(CultureInfo.InvariantCulture)} AS UBIGINT) * 3600000000000)
                           {agentFilter}
                           GROUP BY session_id
                           ORDER BY last_seen_ns DESC
                           LIMIT {boundedLimit.ToString(CultureInfo.InvariantCulture)};
                           """;

        var items = new List<ConversationListItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var sessionId = reader.Col(0).AsString ?? "";
            var spanCount = reader.Col(1).GetInt64(0);
            var firstNs = reader.Col(2).GetUInt64(0UL);
            var lastNs = reader.Col(3).GetUInt64(0UL);
            var totalCost = reader.Col(4).GetDouble(0d);
            var inputTokens = reader.Col(5).GetInt64(0);
            var outputTokens = reader.Col(6).GetInt64(0);

            var services = reader.Col(7).AsList<string>()?.ToList() ?? [];
            var models = reader.Col(8).AsList<string>()?.ToList() ?? [];

            var first = NanosToUtc(firstNs);
            var last = NanosToUtc(lastNs);

            items.Add(new ConversationListItem(
                sessionId,
                spanCount,
                first,
                last,
                (last - first).TotalMilliseconds,
                totalCost,
                inputTokens > 0 ? inputTokens : null,
                outputTokens > 0 ? outputTokens : null,
                services,
                models));
        }

        return TypedResults.Ok(new ConversationListResponse(items, items.Count, ReadCaptureFlags(configuration)));
    }

    private static async Task<IResult> GetConversationAsync(
        string sessionId,
        DuckDbStore store,
        IConfiguration configuration,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return TypedResults.BadRequest(new { error = "sessionId is required" });

        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT span_id, trace_id, parent_span_id, name, service_name,
                                 start_time_unix_nano, duration_ns,
                                 gen_ai_provider_name, gen_ai_request_model, gen_ai_response_model,
                                 gen_ai_input_tokens, gen_ai_output_tokens,
                                 gen_ai_tool_name, gen_ai_tool_call_id, gen_ai_cost_usd,
                                 status_code, status_message, attributes_json,
                                 json_extract_string(attributes_json, '$."gen_ai.agent.name"') AS agent_name
                          FROM qyl_conversations
                          WHERE session_id = $1
                          ORDER BY start_time_unix_nano ASC;
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = sessionId });

        var spans = new List<ConversationSpan>();
        var firstSeen = DateTime.MaxValue;
        var lastSeen = DateTime.MinValue;
        var totalCost = 0d;
        string? firstAgentName = null;

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var startNs = reader.Col(5).GetUInt64(0UL);
            var durationNs = reader.Col(6).GetUInt64(0UL);
            var startTime = NanosToUtc(startNs);
            var durationMs = durationNs / 1_000_000d;
            var costUsd = reader.Col(14).AsDouble;

            if (startTime < firstSeen) firstSeen = startTime;
            var endTime = NanosToUtc(startNs + durationNs);
            if (endTime > lastSeen) lastSeen = endTime;
            if (costUsd is { } c) totalCost += c;

            firstAgentName ??= reader.Col(18).AsString;

            spans.Add(new ConversationSpan(
                SpanId: reader.Col(0).AsString ?? "",
                TraceId: reader.Col(1).AsString ?? "",
                ParentSpanId: reader.Col(2).AsString,
                Name: reader.Col(3).AsString ?? "",
                ServiceName: reader.Col(4).AsString ?? "",
                StartTime: startTime,
                DurationMs: durationMs,
                Provider: reader.Col(7).AsString,
                RequestModel: reader.Col(8).AsString,
                ResponseModel: reader.Col(9).AsString,
                InputTokens: reader.Col(10).AsInt64,
                OutputTokens: reader.Col(11).AsInt64,
                ToolName: reader.Col(12).AsString,
                ToolCallId: reader.Col(13).AsString,
                CostUsd: costUsd,
                StatusCode: reader.Col(15).GetByte((byte)0),
                StatusMessage: reader.Col(16).AsString,
                Attributes: ParseJson(reader.Col(17).AsString)));
        }

        if (spans.Count == 0)
            return TypedResults.NotFound(new { error = "No conversation found for sessionId", sessionId });

        SpanCountHistogram.Record(
            spans.Count,
            new KeyValuePair<string, object?>("agent_name", firstAgentName ?? "unknown"));

        return TypedResults.Ok(new ConversationDetail(
            sessionId,
            spans.Count,
            firstSeen,
            lastSeen,
            totalCost,
            spans,
            ReadCaptureFlags(configuration)));
    }

    private static ConversationCaptureFlags ReadCaptureFlags(IConfiguration configuration) =>
        new(
            MessageContent: ReadFlag(configuration, "OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT"),
            RecordInputs: ReadFlag(configuration, "QYL_MCP_RECORD_INPUTS"),
            RecordOutputs: ReadFlag(configuration, "QYL_MCP_RECORD_OUTPUTS"));

    private static bool ReadFlag(IConfiguration configuration, string key)
    {
        var raw = configuration[key] ?? Environment.GetEnvironmentVariable(key);
        return bool.TryParse(raw, out var parsed) && parsed;
    }

    private static DateTime NanosToUtc(ulong nanos) =>
        DateTime.UnixEpoch.AddTicks((long)(nanos / 100));

    private static JsonElement? ParseJson(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
