// =============================================================================
// AnalyticsQueryService - AI chat analytics queries against DuckDB spans
// =============================================================================

using qyl.collector.Core;

namespace qyl.collector.Query;

/// <summary>
///     Analytics query service for AI chat conversations.
///     All queries run against the existing spans table using DuckDB SQL.
///     Uses COALESCE for conversation grouping: attributes_json gen_ai.conversation.id -> session_id -> trace_id.
/// </summary>
public sealed class AnalyticsQueryService(DuckDbStore store)
{
    // =========================================================================
    // Shared SQL fragments
    // =========================================================================

    /// <summary>
    ///     Conversation ID resolution: gen_ai.conversation.id from attributes_json,
    ///     falling back to session_id, then trace_id.
    /// </summary>
    private const string ConversationIdExpr =
        "COALESCE(attributes_json->>'gen_ai.conversation.id', session_id, trace_id)";

    /// <summary>
    ///     Operation name from attributes_json (not a promoted column).
    /// </summary>
    private const string OperationNameExpr =
        "attributes_json->>'gen_ai.operation.name'";

    /// <summary>
    ///     End user ID from attributes_json.
    /// </summary>
    private const string EnduserIdExpr =
        "attributes_json->>'enduser.id'";

    /// <summary>
    ///     Data source ID from attributes_json.
    /// </summary>
    private const string DataSourceIdExpr =
        "attributes_json->>'gen_ai.data_source.id'";

    /// <summary>
    ///     Duration in milliseconds computed from nanoseconds.
    /// </summary>
    private const string DurationMsExpr = "duration_ns / 1000000.0";

    // =========================================================================
    // Period helpers
    // =========================================================================

    private static (ulong StartNano, ulong EndNano) ResolvePeriod(string period, int offset)
    {
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;

        var (start, end) = period.ToLowerInvariant() switch
        {
            "weekly" => GetWeekBounds(now, offset),
            "monthly" => GetMonthBounds(now, offset),
            "quarterly" => GetQuarterBounds(now, offset),
            _ when period.Length == 7 => ParseYearMonth(period), // "2026-02"
            _ => GetMonthBounds(now, offset) // default to monthly
        };

        return (DateTimeToUnixNano(start), DateTimeToUnixNano(end));
    }

    private static (DateTime Start, DateTime End) GetWeekBounds(DateTime now, int offset)
    {
        var startOfWeek = now.Date.AddDays(-(int)now.DayOfWeek + (int)DayOfWeek.Monday);
        startOfWeek = startOfWeek.AddDays(-7 * offset);
        return (startOfWeek, startOfWeek.AddDays(7));
    }

    private static (DateTime Start, DateTime End) GetMonthBounds(DateTime now, int offset)
    {
        var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-offset);
        return (start, start.AddMonths(1));
    }

    private static (DateTime Start, DateTime End) GetQuarterBounds(DateTime now, int offset)
    {
        var quarterMonth = ((now.Month - 1) / 3) * 3 + 1;
        var start = new DateTime(now.Year, quarterMonth, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-3 * offset);
        return (start, start.AddMonths(3));
    }

    private static (DateTime Start, DateTime End) ParseYearMonth(string period)
    {
        var parts = period.Split('-');
        var year = int.Parse(parts[0], CultureInfo.InvariantCulture);
        var month = int.Parse(parts[1], CultureInfo.InvariantCulture);
        var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        return (start, start.AddMonths(1));
    }

    private static ulong DateTimeToUnixNano(DateTime dt)
    {
        var ticks = (dt.ToUniversalTime() - DateTime.UnixEpoch).Ticks;
        return (ulong)ticks * 100UL;
    }

    // =========================================================================
    // 1. List Conversations
    // =========================================================================

    public async Task<ConversationListResult> ListConversationsAsync(
        string period = "monthly",
        int offset = 0,
        int page = 1,
        int pageSize = 20,
        bool? hasErrors = null,
        string? userId = null,
        string? model = null,
        CancellationToken ct = default)
    {
        var (startNano, endNano) = ResolvePeriod(period, offset);
        var skip = (page - 1) * pageSize;

        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();

        cmd.CommandText = $"""
            WITH convos AS (
                SELECT
                    {ConversationIdExpr} AS conversation_id,
                    MIN(start_time_unix_nano) AS started_at,
                    MAX(end_time_unix_nano) AS ended_at,
                    COUNT(*) AS turn_count,
                    SUM(CASE WHEN status_code = 2 THEN 1 ELSE 0 END) AS error_count,
                    COALESCE(SUM(gen_ai_input_tokens), 0) AS total_input_tokens,
                    COALESCE(SUM(gen_ai_output_tokens), 0) AS total_output_tokens,
                    MAX({EnduserIdExpr}) AS user_id,
                    MIN(name) AS first_question
                FROM spans
                WHERE {OperationNameExpr} IS NOT NULL
                  AND start_time_unix_nano >= $1
                  AND start_time_unix_nano < $2
                  AND ($3::VARCHAR IS NULL OR {EnduserIdExpr} IS NOT DISTINCT FROM $3)
                  AND ($4::VARCHAR IS NULL OR gen_ai_request_model IS NOT DISTINCT FROM $4)
                GROUP BY conversation_id
            )
            SELECT conversation_id, started_at, ended_at, turn_count, error_count,
                   total_input_tokens, total_output_tokens, user_id, first_question
            FROM convos
            WHERE ($5::BOOLEAN IS NULL OR (error_count > 0) = $5)
            ORDER BY started_at DESC
            LIMIT $6 OFFSET $7
            """;

        cmd.Parameters.Add(new DuckDBParameter { Value = (decimal)startNano });
        cmd.Parameters.Add(new DuckDBParameter { Value = (decimal)endNano });
        cmd.Parameters.Add(new DuckDBParameter { Value = userId ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = model ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = hasErrors.HasValue ? (object)hasErrors.Value : DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = pageSize });
        cmd.Parameters.Add(new DuckDBParameter { Value = skip });

        var conversations = new List<ConversationSummary>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);

        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var startedAt = reader.Col(1).GetUInt64(0);
            var endedAt = reader.Col(2).GetUInt64(0);

            conversations.Add(new ConversationSummary
            {
                ConversationId = reader.GetString(0),
                StartTime = TimeConversions.UnixNanoToDateTime(startedAt),
                DurationMs = (endedAt - startedAt) / 1_000_000.0,
                TurnCount = reader.Col(3).GetInt64(0),
                ErrorCount = reader.Col(4).GetInt64(0),
                HasErrors = reader.Col(4).GetInt64(0) > 0,
                TotalInputTokens = reader.Col(5).GetInt64(0),
                TotalOutputTokens = reader.Col(6).GetInt64(0),
                UserId = reader.Col(7).AsString,
                FirstQuestion = reader.Col(8).AsString
            });
        }

        // Get total count
        await using var countCmd = lease.Connection.CreateCommand();
        countCmd.CommandText = $"""
            SELECT COUNT(DISTINCT {ConversationIdExpr})
            FROM spans
            WHERE {OperationNameExpr} IS NOT NULL
              AND start_time_unix_nano >= $1
              AND start_time_unix_nano < $2
            """;
        countCmd.Parameters.Add(new DuckDBParameter { Value = (decimal)startNano });
        countCmd.Parameters.Add(new DuckDBParameter { Value = (decimal)endNano });

        var total = (long)(await countCmd.ExecuteScalarAsync(ct).ConfigureAwait(false) ?? 0L);

        return new ConversationListResult
        {
            Conversations = conversations,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    // =========================================================================
    // 2. Get Conversation Detail
    // =========================================================================

    public async Task<ConversationDetail?> GetConversationAsync(
        string conversationId,
        CancellationToken ct = default)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();

        cmd.CommandText = $"""
            SELECT
                span_id, name, start_time_unix_nano, end_time_unix_nano, duration_ns,
                status_code, status_message,
                gen_ai_provider_name, gen_ai_request_model,
                gen_ai_input_tokens, gen_ai_output_tokens,
                gen_ai_tool_name, gen_ai_stop_reason,
                {OperationNameExpr} AS operation_name,
                {EnduserIdExpr} AS enduser_id,
                {DataSourceIdExpr} AS data_source_id
            FROM spans
            WHERE {ConversationIdExpr} = $1
            ORDER BY start_time_unix_nano ASC
            """;

        cmd.Parameters.Add(new DuckDBParameter { Value = conversationId });

        var turns = new List<ConversationTurn>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);

        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var startNano = reader.Col(2).GetUInt64(0);

            turns.Add(new ConversationTurn
            {
                SpanId = reader.GetString(0),
                Name = reader.GetString(1),
                Timestamp = TimeConversions.UnixNanoToDateTime(startNano),
                DurationMs = reader.Col(4).GetUInt64(0) / 1_000_000.0,
                StatusCode = reader.Col(5).GetByte(0),
                StatusMessage = reader.Col(6).AsString,
                Provider = reader.Col(7).AsString,
                Model = reader.Col(8).AsString,
                InputTokens = reader.Col(9).GetInt64(0),
                OutputTokens = reader.Col(10).GetInt64(0),
                ToolName = reader.Col(11).AsString,
                StopReason = reader.Col(12).AsString,
                OperationName = reader.Col(13).AsString,
                UserId = reader.Col(14).AsString,
                DataSourceId = reader.Col(15).AsString
            });
        }

        if (turns.Count is 0)
            return null;

        return new ConversationDetail
        {
            ConversationId = conversationId,
            Turns = turns
        };
    }

    // =========================================================================
    // 3. Coverage Gaps
    // =========================================================================

    public async Task<CoverageGapsResult> GetCoverageGapsAsync(
        string period = "monthly",
        int offset = 0,
        CancellationToken ct = default)
    {
        var (startNano, endNano) = ResolvePeriod(period, offset);

        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);

        // First get total conversation count for the period
        await using var countCmd = lease.Connection.CreateCommand();
        countCmd.CommandText = $"""
            SELECT COUNT(DISTINCT {ConversationIdExpr})
            FROM spans
            WHERE {OperationNameExpr} IS NOT NULL
              AND start_time_unix_nano >= $1
              AND start_time_unix_nano < $2
            """;
        countCmd.Parameters.Add(new DuckDBParameter { Value = (decimal)startNano });
        countCmd.Parameters.Add(new DuckDBParameter { Value = (decimal)endNano });
        var totalConversations = (long)(await countCmd.ExecuteScalarAsync(ct).ConfigureAwait(false) ?? 0L);

        // Find uncertain conversations grouped by span name pattern
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = $"""
            WITH uncertain AS (
                SELECT
                    {ConversationIdExpr} AS conversation_id,
                    name AS span_name,
                    COUNT(*) AS span_count,
                    SUM(CASE WHEN status_code = 2 THEN 1 ELSE 0 END) AS error_count,
                    COALESCE(SUM(gen_ai_input_tokens + gen_ai_output_tokens), 0) AS total_tokens,
                    MAX({DurationMsExpr}) AS max_duration_ms
                FROM spans
                WHERE {OperationNameExpr} IS NOT NULL
                  AND start_time_unix_nano >= $1
                  AND start_time_unix_nano < $2
                  AND (
                      status_code = 2
                      OR gen_ai_output_tokens = 0
                      OR {DurationMsExpr} > (
                          SELECT COALESCE(percentile_disc(0.95) WITHIN GROUP (ORDER BY {DurationMsExpr}), 999999)
                          FROM spans
                          WHERE {OperationNameExpr} IS NOT NULL
                            AND start_time_unix_nano >= $1
                            AND start_time_unix_nano < $2
                      )
                  )
                GROUP BY conversation_id, span_name
            )
            SELECT
                span_name AS topic,
                COUNT(DISTINCT conversation_id) AS conversation_count,
                array_agg(DISTINCT conversation_id ORDER BY conversation_id) AS sample_ids,
                SUM(error_count) AS total_errors,
                MAX(max_duration_ms) AS max_duration_ms
            FROM uncertain
            GROUP BY span_name
            HAVING COUNT(DISTINCT conversation_id) >= 2
            ORDER BY conversation_count DESC
            LIMIT 50
            """;

        cmd.Parameters.Add(new DuckDBParameter { Value = (decimal)startNano });
        cmd.Parameters.Add(new DuckDBParameter { Value = (decimal)endNano });

        var gaps = new List<CoverageGap>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);

        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var topic = reader.Col(0).AsString ?? "unknown";
            var conversationCount = reader.Col(1).GetInt64(0);
            var sampleIds = ReadStringList(reader, 2);
            var totalErrors = reader.Col(3).GetInt64(0);

            gaps.Add(new CoverageGap
            {
                Topic = topic,
                ConversationCount = conversationCount,
                Finding = totalErrors > 0
                    ? $"{conversationCount} conversations about '{topic}' had errors or uncertain outcomes"
                    : $"{conversationCount} conversations about '{topic}' had high latency or empty responses",
                Recommendation = $"Review documentation coverage for '{topic}' and add targeted content",
                SampleConversationIds = sampleIds.Take(5).ToList()
            });
        }

        return new CoverageGapsResult
        {
            ConversationsProcessed = totalConversations,
            GapsIdentified = gaps.Count,
            Gaps = gaps
        };
    }

    // =========================================================================
    // 4. Top Questions
    // =========================================================================

    public async Task<TopQuestionsResult> GetTopQuestionsAsync(
        string period = "monthly",
        int offset = 0,
        int minConversations = 3,
        CancellationToken ct = default)
    {
        var (startNano, endNano) = ResolvePeriod(period, offset);

        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);

        // Total conversations
        await using var countCmd = lease.Connection.CreateCommand();
        countCmd.CommandText = $"""
            SELECT COUNT(DISTINCT {ConversationIdExpr})
            FROM spans
            WHERE {OperationNameExpr} IS NOT NULL
              AND start_time_unix_nano >= $1
              AND start_time_unix_nano < $2
            """;
        countCmd.Parameters.Add(new DuckDBParameter { Value = (decimal)startNano });
        countCmd.Parameters.Add(new DuckDBParameter { Value = (decimal)endNano });
        var totalConversations = (long)(await countCmd.ExecuteScalarAsync(ct).ConfigureAwait(false) ?? 0L);

        // Group by span name (topic proxy)
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT
                name AS topic,
                COUNT(DISTINCT {ConversationIdExpr}) AS conversation_count,
                array_agg(DISTINCT {ConversationIdExpr} ORDER BY {ConversationIdExpr}) AS sample_ids
            FROM spans
            WHERE {OperationNameExpr} IS NOT NULL
              AND start_time_unix_nano >= $1
              AND start_time_unix_nano < $2
            GROUP BY name
            HAVING COUNT(DISTINCT {ConversationIdExpr}) >= $3
            ORDER BY conversation_count DESC
            LIMIT 50
            """;

        cmd.Parameters.Add(new DuckDBParameter { Value = (decimal)startNano });
        cmd.Parameters.Add(new DuckDBParameter { Value = (decimal)endNano });
        cmd.Parameters.Add(new DuckDBParameter { Value = minConversations });

        var clusters = new List<TopQuestionCluster>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);

        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var sampleIds = ReadStringList(reader, 2);

            clusters.Add(new TopQuestionCluster
            {
                Topic = reader.Col(0).AsString ?? "unknown",
                ConversationCount = reader.Col(1).GetInt64(0),
                SampleConversationIds = sampleIds.Take(5).ToList()
            });
        }

        return new TopQuestionsResult
        {
            ConversationsProcessed = totalConversations,
            ClustersIdentified = clusters.Count,
            Clusters = clusters
        };
    }

    // =========================================================================
    // 5. Source Analytics
    // =========================================================================

    public async Task<SourceAnalyticsResult> GetSourceAnalyticsAsync(
        string period = "monthly",
        int offset = 0,
        CancellationToken ct = default)
    {
        var (startNano, endNano) = ResolvePeriod(period, offset);

        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();

        cmd.CommandText = $"""
            SELECT
                {DataSourceIdExpr} AS source_id,
                COUNT(*) AS citation_count,
                array_agg(DISTINCT name ORDER BY name) AS top_questions
            FROM spans
            WHERE {DataSourceIdExpr} IS NOT NULL
              AND start_time_unix_nano >= $1
              AND start_time_unix_nano < $2
            GROUP BY source_id
            ORDER BY citation_count DESC
            LIMIT 100
            """;

        cmd.Parameters.Add(new DuckDBParameter { Value = (decimal)startNano });
        cmd.Parameters.Add(new DuckDBParameter { Value = (decimal)endNano });

        var sources = new List<SourceUsage>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);

        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var topQuestions = ReadStringList(reader, 2);

            sources.Add(new SourceUsage
            {
                SourceId = reader.Col(0).AsString ?? "unknown",
                CitationCount = reader.Col(1).GetInt64(0),
                TopQuestions = topQuestions.Take(5).ToList()
            });
        }

        return new SourceAnalyticsResult
        {
            Sources = sources
        };
    }

    // =========================================================================
    // 6. Satisfaction
    // =========================================================================

    public async Task<SatisfactionResult> GetSatisfactionAsync(
        string period = "monthly",
        int offset = 0,
        CancellationToken ct = default)
    {
        var (startNano, endNano) = ResolvePeriod(period, offset);

        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();

        // Aggregate feedback from attributes_json
        cmd.CommandText = $"""
            SELECT
                COUNT(*) AS total_feedback,
                SUM(CASE WHEN attributes_json->>'qyl.feedback.reaction' = 'upvote' THEN 1 ELSE 0 END) AS upvotes,
                SUM(CASE WHEN attributes_json->>'qyl.feedback.reaction' = 'downvote' THEN 1 ELSE 0 END) AS downvotes
            FROM spans
            WHERE attributes_json->>'qyl.feedback.reaction' IS NOT NULL
              AND start_time_unix_nano >= $1
              AND start_time_unix_nano < $2
            """;

        cmd.Parameters.Add(new DuckDBParameter { Value = (decimal)startNano });
        cmd.Parameters.Add(new DuckDBParameter { Value = (decimal)endNano });

        long totalFeedback = 0, upvotes = 0, downvotes = 0;
        await using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            if (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                totalFeedback = reader.Col(0).GetInt64(0);
                upvotes = reader.Col(1).GetInt64(0);
                downvotes = reader.Col(2).GetInt64(0);
            }
        }

        // Satisfaction by model
        await using var modelCmd = lease.Connection.CreateCommand();
        modelCmd.CommandText = $"""
            SELECT
                gen_ai_request_model AS model,
                COUNT(*) AS feedback_count,
                SUM(CASE WHEN attributes_json->>'qyl.feedback.reaction' = 'upvote' THEN 1 ELSE 0 END) AS up,
                SUM(CASE WHEN attributes_json->>'qyl.feedback.reaction' = 'downvote' THEN 1 ELSE 0 END) AS down
            FROM spans
            WHERE attributes_json->>'qyl.feedback.reaction' IS NOT NULL
              AND gen_ai_request_model IS NOT NULL
              AND start_time_unix_nano >= $1
              AND start_time_unix_nano < $2
            GROUP BY gen_ai_request_model
            ORDER BY feedback_count DESC
            """;

        modelCmd.Parameters.Add(new DuckDBParameter { Value = (decimal)startNano });
        modelCmd.Parameters.Add(new DuckDBParameter { Value = (decimal)endNano });

        var byModel = new List<SatisfactionByModel>();
        await using (var modelReader = await modelCmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            while (await modelReader.ReadAsync(ct).ConfigureAwait(false))
            {
                var up = modelReader.Col(2).GetInt64(0);
                var down = modelReader.Col(3).GetInt64(0);
                var total = up + down;

                byModel.Add(new SatisfactionByModel
                {
                    Model = modelReader.Col(0).AsString ?? "unknown",
                    Rate = total > 0 ? (double)up / total : 0,
                    Downvotes = down
                });
            }
        }

        // Satisfaction by topic (span name)
        await using var topicCmd = lease.Connection.CreateCommand();
        topicCmd.CommandText = $"""
            SELECT
                name AS topic,
                COUNT(*) AS feedback_count,
                SUM(CASE WHEN attributes_json->>'qyl.feedback.reaction' = 'upvote' THEN 1 ELSE 0 END) AS up,
                SUM(CASE WHEN attributes_json->>'qyl.feedback.reaction' = 'downvote' THEN 1 ELSE 0 END) AS down
            FROM spans
            WHERE attributes_json->>'qyl.feedback.reaction' IS NOT NULL
              AND start_time_unix_nano >= $1
              AND start_time_unix_nano < $2
            GROUP BY name
            HAVING SUM(CASE WHEN attributes_json->>'qyl.feedback.reaction' = 'downvote' THEN 1 ELSE 0 END) > 0
            ORDER BY down DESC
            LIMIT 20
            """;

        topicCmd.Parameters.Add(new DuckDBParameter { Value = (decimal)startNano });
        topicCmd.Parameters.Add(new DuckDBParameter { Value = (decimal)endNano });

        var byTopic = new List<SatisfactionByTopic>();
        await using (var topicReader = await topicCmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            while (await topicReader.ReadAsync(ct).ConfigureAwait(false))
            {
                var up = topicReader.Col(2).GetInt64(0);
                var down = topicReader.Col(3).GetInt64(0);
                var total = up + down;

                byTopic.Add(new SatisfactionByTopic
                {
                    Topic = topicReader.Col(0).AsString ?? "unknown",
                    Rate = total > 0 ? (double)up / total : 0,
                    Downvotes = down
                });
            }
        }

        var satisfactionRate = totalFeedback > 0 ? (double)upvotes / totalFeedback : 0;

        return new SatisfactionResult
        {
            TotalFeedback = totalFeedback,
            Upvotes = upvotes,
            Downvotes = downvotes,
            SatisfactionRate = satisfactionRate,
            ByModel = byModel,
            ByTopic = byTopic
        };
    }

    // =========================================================================
    // 7. List Users
    // =========================================================================

    public async Task<UserListResult> ListUsersAsync(
        string period = "monthly",
        int offset = 0,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var (startNano, endNano) = ResolvePeriod(period, offset);
        var skip = (page - 1) * pageSize;

        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();

        cmd.CommandText = $"""
            SELECT
                {EnduserIdExpr} AS user_id,
                COUNT(DISTINCT {ConversationIdExpr}) AS conversation_count,
                MIN(start_time_unix_nano) AS first_seen,
                MAX(end_time_unix_nano) AS last_seen,
                array_agg(DISTINCT name ORDER BY name) AS top_topics
            FROM spans
            WHERE {EnduserIdExpr} IS NOT NULL
              AND {OperationNameExpr} IS NOT NULL
              AND start_time_unix_nano >= $1
              AND start_time_unix_nano < $2
            GROUP BY user_id
            ORDER BY conversation_count DESC
            LIMIT $3 OFFSET $4
            """;

        cmd.Parameters.Add(new DuckDBParameter { Value = (decimal)startNano });
        cmd.Parameters.Add(new DuckDBParameter { Value = (decimal)endNano });
        cmd.Parameters.Add(new DuckDBParameter { Value = pageSize });
        cmd.Parameters.Add(new DuckDBParameter { Value = skip });

        var users = new List<UserSummary>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);

        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var firstSeen = reader.Col(2).GetUInt64(0);
            var lastSeen = reader.Col(3).GetUInt64(0);
            var topics = ReadStringList(reader, 4);

            users.Add(new UserSummary
            {
                UserId = reader.GetString(0),
                ConversationCount = reader.Col(1).GetInt64(0),
                FirstSeen = TimeConversions.UnixNanoToDateTime(firstSeen),
                LastSeen = TimeConversions.UnixNanoToDateTime(lastSeen),
                TopTopics = topics.Take(5).ToList()
            });
        }

        // Total user count
        await using var countCmd = lease.Connection.CreateCommand();
        countCmd.CommandText = $"""
            SELECT COUNT(DISTINCT {EnduserIdExpr})
            FROM spans
            WHERE {EnduserIdExpr} IS NOT NULL
              AND {OperationNameExpr} IS NOT NULL
              AND start_time_unix_nano >= $1
              AND start_time_unix_nano < $2
            """;
        countCmd.Parameters.Add(new DuckDBParameter { Value = (decimal)startNano });
        countCmd.Parameters.Add(new DuckDBParameter { Value = (decimal)endNano });
        var total = (long)(await countCmd.ExecuteScalarAsync(ct).ConfigureAwait(false) ?? 0L);

        return new UserListResult
        {
            Users = users,
            Total = total
        };
    }

    // =========================================================================
    // 8. Get User Journey
    // =========================================================================

    public async Task<UserJourneyResult?> GetUserJourneyAsync(
        string userId,
        CancellationToken ct = default)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();

        cmd.CommandText = $"""
            SELECT
                {ConversationIdExpr} AS conversation_id,
                MIN(start_time_unix_nano) AS started_at,
                MIN(name) AS topic,
                COUNT(*) AS turn_count,
                SUM(CASE WHEN status_code = 2 THEN 1 ELSE 0 END) AS error_count,
                COALESCE(SUM(gen_ai_input_tokens), 0) + COALESCE(SUM(gen_ai_output_tokens), 0) AS total_tokens
            FROM spans
            WHERE {EnduserIdExpr} = $1
              AND {OperationNameExpr} IS NOT NULL
            GROUP BY conversation_id
            ORDER BY started_at DESC
            LIMIT 100
            """;

        cmd.Parameters.Add(new DuckDBParameter { Value = userId });

        var conversations = new List<UserConversation>();
        long totalTokens = 0;
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);

        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var startedAt = reader.Col(1).GetUInt64(0);
            var tokens = reader.Col(5).GetInt64(0);
            totalTokens += tokens;

            conversations.Add(new UserConversation
            {
                ConversationId = reader.GetString(0),
                Date = TimeConversions.UnixNanoToDateTime(startedAt),
                Topic = reader.Col(2).AsString,
                TurnCount = reader.Col(3).GetInt64(0),
                Satisfied = reader.Col(4).GetInt64(0) == 0
            });
        }

        if (conversations.Count is 0)
            return null;

        // Frequent topics
        var frequentTopics = conversations
            .Where(c => c.Topic is not null)
            .GroupBy(c => c.Topic!)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => g.Key)
            .ToList();

        // Retention days
        var retentionDays = conversations.Count >= 2
            ? (int)(conversations[0].Date - conversations[^1].Date).TotalDays
            : 0;

        return new UserJourneyResult
        {
            UserId = userId,
            Conversations = conversations,
            TotalTokens = totalTokens,
            FrequentTopics = frequentTopics,
            RetentionDays = retentionDays
        };
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static IReadOnlyList<string> ReadStringList(DbDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
            return [];

        var value = reader.GetValue(ordinal);
        return value switch
        {
            IReadOnlyList<string> list => list,
            object[] arr => Array.ConvertAll(arr, static x => x.ToString() ?? ""),
            _ => []
        };
    }
}

// =============================================================================
// DTOs
// =============================================================================

// --- Conversations ---

public sealed record ConversationListResult
{
    public IReadOnlyList<ConversationSummary> Conversations { get; init; } = [];
    public long Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed record ConversationSummary
{
    public required string ConversationId { get; init; }
    public DateTime StartTime { get; init; }
    public double DurationMs { get; init; }
    public long TurnCount { get; init; }
    public long ErrorCount { get; init; }
    public bool HasErrors { get; init; }
    public long TotalInputTokens { get; init; }
    public long TotalOutputTokens { get; init; }
    public string? UserId { get; init; }
    public string? FirstQuestion { get; init; }
}

public sealed record ConversationDetail
{
    public required string ConversationId { get; init; }
    public IReadOnlyList<ConversationTurn> Turns { get; init; } = [];
}

public sealed record ConversationTurn
{
    public required string SpanId { get; init; }
    public required string Name { get; init; }
    public DateTime Timestamp { get; init; }
    public double DurationMs { get; init; }
    public byte StatusCode { get; init; }
    public string? StatusMessage { get; init; }
    public string? Provider { get; init; }
    public string? Model { get; init; }
    public long InputTokens { get; init; }
    public long OutputTokens { get; init; }
    public string? ToolName { get; init; }
    public string? StopReason { get; init; }
    public string? OperationName { get; init; }
    public string? UserId { get; init; }
    public string? DataSourceId { get; init; }
}

// --- Coverage Gaps ---

public sealed record CoverageGapsResult
{
    public long ConversationsProcessed { get; init; }
    public int GapsIdentified { get; init; }
    public IReadOnlyList<CoverageGap> Gaps { get; init; } = [];
}

public sealed record CoverageGap
{
    public required string Topic { get; init; }
    public long ConversationCount { get; init; }
    public string? Finding { get; init; }
    public string? Recommendation { get; init; }
    public IReadOnlyList<string> SampleConversationIds { get; init; } = [];
}

// --- Top Questions ---

public sealed record TopQuestionsResult
{
    public long ConversationsProcessed { get; init; }
    public int ClustersIdentified { get; init; }
    public IReadOnlyList<TopQuestionCluster> Clusters { get; init; } = [];
}

public sealed record TopQuestionCluster
{
    public required string Topic { get; init; }
    public long ConversationCount { get; init; }
    public IReadOnlyList<string> SampleConversationIds { get; init; } = [];
}

// --- Source Analytics ---

public sealed record SourceAnalyticsResult
{
    public IReadOnlyList<SourceUsage> Sources { get; init; } = [];
}

public sealed record SourceUsage
{
    public required string SourceId { get; init; }
    public long CitationCount { get; init; }
    public IReadOnlyList<string> TopQuestions { get; init; } = [];
}

// --- Satisfaction ---

public sealed record SatisfactionResult
{
    public long TotalFeedback { get; init; }
    public long Upvotes { get; init; }
    public long Downvotes { get; init; }
    public double SatisfactionRate { get; init; }
    public IReadOnlyList<SatisfactionByModel> ByModel { get; init; } = [];
    public IReadOnlyList<SatisfactionByTopic> ByTopic { get; init; } = [];
}

public sealed record SatisfactionByModel
{
    public required string Model { get; init; }
    public double Rate { get; init; }
    public long Downvotes { get; init; }
}

public sealed record SatisfactionByTopic
{
    public required string Topic { get; init; }
    public double Rate { get; init; }
    public long Downvotes { get; init; }
}

// --- Users ---

public sealed record UserListResult
{
    public IReadOnlyList<UserSummary> Users { get; init; } = [];
    public long Total { get; init; }
}

public sealed record UserSummary
{
    public required string UserId { get; init; }
    public long ConversationCount { get; init; }
    public DateTime FirstSeen { get; init; }
    public DateTime LastSeen { get; init; }
    public IReadOnlyList<string> TopTopics { get; init; } = [];
}

// --- User Journey ---

public sealed record UserJourneyResult
{
    public required string UserId { get; init; }
    public IReadOnlyList<UserConversation> Conversations { get; init; } = [];
    public long TotalTokens { get; init; }
    public IReadOnlyList<string> FrequentTopics { get; init; } = [];
    public int RetentionDays { get; init; }
}

public sealed record UserConversation
{
    public required string ConversationId { get; init; }
    public DateTime Date { get; init; }
    public string? Topic { get; init; }
    public long TurnCount { get; init; }
    public bool Satisfied { get; init; }
}
