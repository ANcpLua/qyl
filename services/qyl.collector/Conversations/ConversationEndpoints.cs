using Qyl.Collector.Telemetry;

namespace Qyl.Collector.Conversations;

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
    private static readonly Meter s_conversationsMeter = new(QylTelemetry.ConversationsMeterName, QylTelemetry.ServiceVersion);

    private static readonly Histogram<long> s_spanCountHistogram = s_conversationsMeter.CreateHistogram<long>(
        QylTelemetry.ConversationSpanCountMetricName,
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

        return await store.ExecuteReadAsync(con =>
        {
            using var cmd = con.CreateCommand();
            var agentNameExpr = DuckDbJson.ExtractString("attributes_json", SemanticAttributeKeys.GenAiAgentName);

            var agentFilter = string.IsNullOrWhiteSpace(agent)
                ? string.Empty
                : " AND session_id IN (SELECT session_id FROM qyl_conversations"
                  + " WHERE " + agentNameExpr + " = $"
                  + cmd.AddParam(agent) + ")";

            // Keep CommandText free of interpolated strings so the SQL analyzer can validate it.
            var windowParam = cmd.AddParam(window);
            var limitParam = cmd.AddParam(boundedLimit);

            cmd.CommandText =
                "SELECT "
                + "session_id, "
                + "COUNT(*) AS span_count, "
                + "MIN(start_time_unix_nano) AS first_seen_ns, "
                + "MAX(end_time_unix_nano) AS last_seen_ns, "
                + "COALESCE(SUM(gen_ai_cost_usd), 0) AS total_cost_usd, "
                + "COALESCE(SUM(gen_ai_input_tokens), 0) AS input_tokens, "
                + "COALESCE(SUM(gen_ai_output_tokens), 0) AS output_tokens, "
                + "list_distinct(list(service_name)) AS services, "
                + "list_distinct(list(gen_ai_request_model) FILTER (WHERE gen_ai_request_model IS NOT NULL)) AS models "
                + "FROM qyl_conversations "
                + "WHERE start_time_unix_nano > (CAST(epoch_ns(now()) AS UBIGINT) - CAST($" + windowParam + " AS UBIGINT) * 3600000000000)"
                + agentFilter
                + " GROUP BY session_id"
                + " ORDER BY last_seen_ns DESC"
                + " LIMIT $" + limitParam + ";";

            var items = new List<ConversationListItem>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
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

            return (IResult)TypedResults.Ok(new ConversationListResponse(items, items.Count, ReadCaptureFlags(configuration)));
        }, ct);
    }

    private static async Task<IResult> GetConversationAsync(
        string sessionId,
        DuckDbStore store,
        IConfiguration configuration,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return TypedResults.BadRequest(new { error = "sessionId is required" });

        var (spans, firstSeen, lastSeen, totalCost, firstAgentName) = await store.ExecuteReadAsync(con =>
        {
            using var cmd = con.CreateCommand();
            var agentNameExpr = DuckDbJson.ExtractString("attributes_json", SemanticAttributeKeys.GenAiAgentName);
            cmd.CommandText =
                "SELECT span_id, trace_id, parent_span_id, name, service_name, "
                + "start_time_unix_nano, duration_ns, "
                + "gen_ai_provider_name, gen_ai_request_model, gen_ai_response_model, "
                + "gen_ai_input_tokens, gen_ai_output_tokens, "
                + "gen_ai_tool_name, gen_ai_tool_call_id, gen_ai_cost_usd, "
                + "status_code, status_message, attributes_json, "
                + agentNameExpr + " AS agent_name "
                + "FROM qyl_conversations "
                + "WHERE session_id = $1 "
                + "ORDER BY start_time_unix_nano ASC;";
            cmd.Parameters.Add(new DuckDBParameter { Value = sessionId });

            var rows = new List<ConversationSpan>();
            var first = DateTime.MaxValue;
            var last = DateTime.MinValue;
            var cost = 0d;
            string? agentName = null;

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var startNs = reader.Col(5).GetUInt64(0UL);
                var durationNs = reader.Col(6).GetUInt64(0UL);
                var startTime = NanosToUtc(startNs);
                var durationMs = durationNs / 1_000_000d;
                var costUsd = reader.Col(14).AsDouble;

                if (startTime < first) first = startTime;
                var endTime = NanosToUtc(startNs + durationNs);
                if (endTime > last) last = endTime;
                if (costUsd is { } c) cost += c;

                agentName ??= reader.Col(18).AsString;

                rows.Add(new ConversationSpan(
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
                    StatusCode: ReadStatusCode(reader, 15),
                    StatusMessage: reader.Col(16).AsString,
                    Attributes: ParseJson(reader.Col(17).AsString)));
            }

            return (rows, first, last, cost, agentName);
        }, ct);

        if (spans.Count == 0)
            return TypedResults.NotFound(new { error = "No conversation found for sessionId", sessionId });

        s_spanCountHistogram.Record(spans.Count);

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

    private static int ReadStatusCode(DbDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal)) return 0;

        var value = reader.GetValue(ordinal);
        return value switch
        {
            byte status => status,
            sbyte status => status,
            short status => status,
            ushort status => status,
            int status => status,
            uint status when status <= int.MaxValue => (int)status,
            long status when status is >= int.MinValue and <= int.MaxValue => (int)status,
            ulong status when status <= int.MaxValue => (int)status,
            string raw when int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => 0
        };
    }

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
