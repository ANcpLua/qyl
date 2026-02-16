using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tools for AI chat analytics.
///     Provides conversation browsing, coverage gap detection, topic clustering,
///     source analytics, user satisfaction tracking, and user journey analysis.
/// </summary>
[McpServerToolType]
public sealed class AnalyticsTools(HttpClient client)
{
    [McpServerTool(Name = "qyl.list_conversations")]
    [Description("""
                 List AI conversations captured by qyl.

                 Groups spans by conversation ID to reconstruct chat threads.
                 Each conversation shows: first question, turn count, token usage, errors.

                 Example queries:
                 - This month: list_conversations()
                 - Last month: list_conversations(period="monthly", offset=1)
                 - Specific month: list_conversations(period="2026-02")
                 - With errors only: list_conversations(hasErrors=true)
                 - By user: list_conversations(userId="user@example.com")

                 Returns: Paginated list of conversations with metadata
                 """)]
    public Task<string> ListConversationsAsync(
        [Description("Period: 'weekly', 'monthly', 'quarterly', or 'YYYY-MM' (default: monthly)")]
        string period = "monthly",
        [Description("Period offset (0=current, 1=previous, etc.)")]
        int offset = 0,
        [Description("Page number (default: 1)")]
        int page = 1,
        [Description("Results per page (default: 20, max: 100)")]
        int pageSize = 20,
        [Description("Filter: only conversations with errors")]
        bool? hasErrors = null,
        [Description("Filter: by user ID")] string? userId = null,
        [Description("Filter: by model name")] string? model = null) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var url =
                $"/api/v1/analytics/conversations?period={Uri.EscapeDataString(period)}&offset={offset}&page={page}&pageSize={pageSize}";
            if (hasErrors.HasValue) url += $"&hasErrors={hasErrors.Value}";
            if (!string.IsNullOrEmpty(userId)) url += $"&userId={Uri.EscapeDataString(userId)}";
            if (!string.IsNullOrEmpty(model)) url += $"&model={Uri.EscapeDataString(model)}";

            var response = await client.GetFromJsonAsync<ConversationListDto>(
                url, AnalyticsJsonContext.Default.ConversationListDto).ConfigureAwait(false);

            if (response?.Conversations is null || response.Conversations.Count is 0)
                return "No conversations found for the specified period.";

            var sb = new StringBuilder();
            sb.AppendLine(
                $"# Conversations (page {response.Page}/{Math.Max(1, (response.Total + response.PageSize - 1) / response.PageSize)}, total: {response.Total})");
            sb.AppendLine();

            foreach (var conv in response.Conversations)
            {
                var errorTag = conv.HasErrors ? " [ERRORS]" : "";
                sb.AppendLine($"## {conv.ConversationId}{errorTag}");
                if (!string.IsNullOrEmpty(conv.FirstQuestion))
                    sb.AppendLine($"- First question: {conv.FirstQuestion}");
                sb.AppendLine($"- Turns: {conv.TurnCount}");
                sb.AppendLine($"- Tokens: {conv.TotalInputTokens} in / {conv.TotalOutputTokens} out");
                if (conv.ErrorCount > 0)
                    sb.AppendLine($"- Errors: {conv.ErrorCount}");
                if (!string.IsNullOrEmpty(conv.UserId))
                    sb.AppendLine($"- User: {conv.UserId}");
                sb.AppendLine($"- Started: {conv.StartTime:u}");
                sb.AppendLine();
            }

            return sb.ToString();
        }, "Error fetching conversations");

    [McpServerTool(Name = "qyl.get_conversation")]
    [Description("""
                 Get the full detail of a single AI conversation.

                 Shows all turns in chronological order with:
                 - Operation type (chat, embeddings, tool call)
                 - Provider and model used
                 - Token counts and duration
                 - Error details if any

                 Use list_conversations first to find a conversation ID.

                 Returns: Complete conversation timeline
                 """)]
    public Task<string> GetConversationAsync(
        [Description("The conversation ID from list_conversations (required)")]
        string conversationId) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var response = await client.GetFromJsonAsync<ConversationDetailDto>(
                $"/api/v1/analytics/conversations/{Uri.EscapeDataString(conversationId)}",
                AnalyticsJsonContext.Default.ConversationDetailDto).ConfigureAwait(false);

            if (response?.Turns is null || response.Turns.Count is 0)
                return $"Conversation '{conversationId}' not found.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Conversation: {conversationId}");
            sb.AppendLine($"Total turns: {response.Turns.Count}");
            sb.AppendLine();

            foreach (var turn in response.Turns)
            {
                var statusIcon = turn.StatusCode == 2 ? " [ERROR]" : "";
                sb.AppendLine($"## {turn.Name}{statusIcon}");
                sb.AppendLine($"- Time: {turn.Timestamp:u}");
                sb.AppendLine($"- Duration: {turn.DurationMs:F0}ms");
                if (!string.IsNullOrEmpty(turn.OperationName))
                    sb.AppendLine($"- Operation: {turn.OperationName}");
                if (!string.IsNullOrEmpty(turn.Provider))
                    sb.AppendLine($"- Provider: {turn.Provider}");
                if (!string.IsNullOrEmpty(turn.Model))
                    sb.AppendLine($"- Model: {turn.Model}");
                if (turn.InputTokens > 0 || turn.OutputTokens > 0)
                    sb.AppendLine($"- Tokens: {turn.InputTokens} in / {turn.OutputTokens} out");
                if (!string.IsNullOrEmpty(turn.ToolName))
                    sb.AppendLine($"- Tool: {turn.ToolName}");
                if (turn.StatusCode == 2 && !string.IsNullOrEmpty(turn.StatusMessage))
                    sb.AppendLine($"- Error: {turn.StatusMessage}");
                sb.AppendLine();
            }

            return sb.ToString();
        }, "Error fetching conversation");

    [McpServerTool(Name = "qyl.get_coverage_gaps")]
    [Description("""
                 Identify topics where the AI assistant fails to help users.

                 Analyzes conversations with uncertainty signals:
                 - Error responses
                 - Empty completions (zero output tokens)
                 - High latency (above 95th percentile)

                 Groups recurring failures into topics with conversation counts
                 and sample IDs for investigation.

                 Use this to find documentation gaps and product improvement areas.

                 Returns: List of coverage gaps with findings and recommendations
                 """)]
    public Task<string> GetCoverageGapsAsync(
        [Description("Period: 'weekly', 'monthly', 'quarterly' (default: monthly)")]
        string period = "monthly",
        [Description("Period offset (0=current, 1=previous, etc.)")]
        int offset = 0) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var response = await client.GetFromJsonAsync<CoverageGapsDto>(
                $"/api/v1/analytics/coverage-gaps?period={Uri.EscapeDataString(period)}&offset={offset}",
                AnalyticsJsonContext.Default.CoverageGapsDto).ConfigureAwait(false);

            if (response is null)
                return "No coverage gap data available.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Coverage Gaps ({period})");
            sb.AppendLine($"Conversations analyzed: {response.ConversationsProcessed}");
            sb.AppendLine($"Gaps identified: {response.GapsIdentified}");
            sb.AppendLine();

            if (response.Gaps is null || response.Gaps.Count is 0)
            {
                sb.AppendLine("No coverage gaps detected.");
                return sb.ToString();
            }

            foreach (var gap in response.Gaps)
            {
                sb.AppendLine($"## {gap.Topic} ({gap.ConversationCount} conversations)");
                if (!string.IsNullOrEmpty(gap.Finding))
                    sb.AppendLine($"**Finding:** {gap.Finding}");
                if (!string.IsNullOrEmpty(gap.Recommendation))
                    sb.AppendLine($"**Recommendation:** {gap.Recommendation}");
                if (gap.SampleConversationIds is { Count: > 0 })
                    sb.AppendLine($"Sample IDs: {string.Join(", ", gap.SampleConversationIds)}");
                sb.AppendLine();
            }

            return sb.ToString();
        }, "Error fetching coverage gaps");

    [McpServerTool(Name = "qyl.get_top_questions")]
    [Description("""
                 Identify the most common topics users ask about.

                 Analyzes ALL conversations (not just failures) to find
                 recurring themes. Groups similar questions into topic clusters.

                 Use this to understand what users care about most.

                 Returns: Topic clusters ranked by conversation count
                 """)]
    public Task<string> GetTopQuestionsAsync(
        [Description("Period: 'weekly', 'monthly', 'quarterly' (default: monthly)")]
        string period = "monthly",
        [Description("Period offset (0=current, 1=previous, etc.)")]
        int offset = 0,
        [Description("Minimum conversations to form a cluster (default: 3)")]
        int minConversations = 3) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var response = await client.GetFromJsonAsync<TopQuestionsDto>(
                $"/api/v1/analytics/top-questions?period={Uri.EscapeDataString(period)}&offset={offset}&minConversations={minConversations}",
                AnalyticsJsonContext.Default.TopQuestionsDto).ConfigureAwait(false);

            if (response is null)
                return "No question data available.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Top Questions ({period})");
            sb.AppendLine($"Conversations analyzed: {response.ConversationsProcessed}");
            sb.AppendLine($"Clusters found: {response.ClustersIdentified}");
            sb.AppendLine();

            if (response.Clusters is null || response.Clusters.Count is 0)
            {
                sb.AppendLine("No question clusters detected.");
                return sb.ToString();
            }

            sb.AppendLine("| Topic | Conversations |");
            sb.AppendLine("|-------|---------------|");

            foreach (var cluster in response.Clusters)
            {
                sb.AppendLine($"| {cluster.Topic} | {cluster.ConversationCount} |");
            }

            return sb.ToString();
        }, "Error fetching top questions");

    [McpServerTool(Name = "qyl.get_source_analytics")]
    [Description("""
                 Show which knowledge sources are most cited by the AI.

                 Tracks which documents/sources the AI references in answers
                 via gen_ai.data_source.id attributes on spans.

                 Use this to identify important content and dead sources.

                 Returns: Sources ranked by citation frequency
                 """)]
    public Task<string> GetSourceAnalyticsAsync(
        [Description("Period: 'weekly', 'monthly', 'quarterly' (default: monthly)")]
        string period = "monthly",
        [Description("Period offset (0=current, 1=previous, etc.)")]
        int offset = 0) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var response = await client.GetFromJsonAsync<SourceAnalyticsDto>(
                $"/api/v1/analytics/source-analytics?period={Uri.EscapeDataString(period)}&offset={offset}",
                AnalyticsJsonContext.Default.SourceAnalyticsDto).ConfigureAwait(false);

            if (response?.Sources is null || response.Sources.Count is 0)
                return "No source analytics data available.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Source Analytics ({period})");
            sb.AppendLine();
            sb.AppendLine("| Source | Citations | Top Questions |");
            sb.AppendLine("|--------|-----------|---------------|");

            foreach (var source in response.Sources)
            {
                var questions = source.TopQuestions is { Count: > 0 }
                    ? string.Join(", ", source.TopQuestions.Take(3))
                    : "-";
                sb.AppendLine($"| {source.SourceId} | {source.CitationCount} | {questions} |");
            }

            return sb.ToString();
        }, "Error fetching source analytics");

    [McpServerTool(Name = "qyl.get_satisfaction")]
    [Description("""
                 Track user satisfaction with AI answers.

                 Aggregates feedback (upvotes/downvotes) from qyl.feedback.reaction
                 attributes on spans. Shows overall satisfaction rate plus breakdowns
                 by model and topic.

                 Use this to monitor answer quality trends.

                 Returns: Satisfaction rate, feedback counts, breakdowns by model and topic
                 """)]
    public Task<string> GetSatisfactionAsync(
        [Description("Period: 'weekly', 'monthly', 'quarterly' (default: monthly)")]
        string period = "monthly",
        [Description("Period offset (0=current, 1=previous, etc.)")]
        int offset = 0) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var response = await client.GetFromJsonAsync<SatisfactionDto>(
                $"/api/v1/analytics/satisfaction?period={Uri.EscapeDataString(period)}&offset={offset}",
                AnalyticsJsonContext.Default.SatisfactionDto).ConfigureAwait(false);

            if (response is null || response.TotalFeedback is 0)
                return "No satisfaction data available for the specified period.";

            var sb = new StringBuilder();
            sb.AppendLine($"# User Satisfaction ({period})");
            sb.AppendLine();
            sb.AppendLine($"- **Total feedback:** {response.TotalFeedback}");
            sb.AppendLine($"- **Upvotes:** {response.Upvotes}");
            sb.AppendLine($"- **Downvotes:** {response.Downvotes}");
            sb.AppendLine($"- **Satisfaction rate:** {response.SatisfactionRate:P1}");
            sb.AppendLine();

            if (response.ByModel is { Count: > 0 })
            {
                sb.AppendLine("## By Model");
                sb.AppendLine("| Model | Rate | Downvotes |");
                sb.AppendLine("|-------|------|-----------|");
                foreach (var m in response.ByModel)
                    sb.AppendLine($"| {m.Model} | {m.Rate:P1} | {m.Downvotes} |");
                sb.AppendLine();
            }

            if (response.ByTopic is { Count: > 0 })
            {
                sb.AppendLine("## Topics with Downvotes");
                sb.AppendLine("| Topic | Rate | Downvotes |");
                sb.AppendLine("|-------|------|-----------|");
                foreach (var t in response.ByTopic)
                    sb.AppendLine($"| {t.Topic} | {t.Rate:P1} | {t.Downvotes} |");
            }

            return sb.ToString();
        }, "Error fetching satisfaction data");

    [McpServerTool(Name = "qyl.list_users")]
    [Description("""
                 List users who have interacted with the AI assistant.

                 Shows user activity: conversation count, first/last seen dates,
                 and most common topics.

                 Uses enduser.id attribute from spans for identity.

                 Returns: Paginated list of users with activity summaries
                 """)]
    public Task<string> ListUsersAsync(
        [Description("Period: 'weekly', 'monthly', 'quarterly' (default: monthly)")]
        string period = "monthly",
        [Description("Period offset (0=current, 1=previous, etc.)")]
        int offset = 0,
        [Description("Page number (default: 1)")]
        int page = 1,
        [Description("Results per page (default: 20)")]
        int pageSize = 20) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var response = await client.GetFromJsonAsync<UserListDto>(
                $"/api/v1/analytics/users?period={Uri.EscapeDataString(period)}&offset={offset}&page={page}&pageSize={pageSize}",
                AnalyticsJsonContext.Default.UserListDto).ConfigureAwait(false);

            if (response?.Users is null || response.Users.Count is 0)
                return "No user data found for the specified period.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Users (total: {response.Total})");
            sb.AppendLine();

            foreach (var user in response.Users)
            {
                sb.AppendLine($"## {user.UserId}");
                sb.AppendLine($"- Conversations: {user.ConversationCount}");
                sb.AppendLine($"- First seen: {user.FirstSeen:u}");
                sb.AppendLine($"- Last seen: {user.LastSeen:u}");
                if (user.TopTopics is { Count: > 0 })
                    sb.AppendLine($"- Topics: {string.Join(", ", user.TopTopics)}");
                sb.AppendLine();
            }

            return sb.ToString();
        }, "Error fetching users");

    [McpServerTool(Name = "qyl.get_user_journey")]
    [Description("""
                 Get an individual user's conversation history and journey.

                 Shows all conversations for a user with topics, turn counts,
                 and satisfaction. Includes aggregate stats like total tokens,
                 frequent topics, and retention days.

                 Use list_users first to find a user ID.

                 Returns: User journey with conversation history and stats
                 """)]
    public Task<string> GetUserJourneyAsync(
        [Description("The user ID from list_users (required)")]
        string userId) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var response = await client.GetFromJsonAsync<UserJourneyDto>(
                $"/api/v1/analytics/users/{Uri.EscapeDataString(userId)}/journey",
                AnalyticsJsonContext.Default.UserJourneyDto).ConfigureAwait(false);

            if (response?.Conversations is null || response.Conversations.Count is 0)
                return $"No journey data found for user '{userId}'.";

            var sb = new StringBuilder();
            sb.AppendLine($"# User Journey: {response.UserId}");
            sb.AppendLine();
            sb.AppendLine($"- **Total conversations:** {response.Conversations.Count}");
            sb.AppendLine($"- **Total tokens:** {response.TotalTokens:N0}");
            sb.AppendLine($"- **Retention:** {response.RetentionDays} days");
            if (response.FrequentTopics is { Count: > 0 })
                sb.AppendLine($"- **Frequent topics:** {string.Join(", ", response.FrequentTopics)}");
            sb.AppendLine();

            sb.AppendLine("## Conversations");
            sb.AppendLine("| Date | Topic | Turns | Satisfied |");
            sb.AppendLine("|------|-------|-------|-----------|");

            foreach (var conv in response.Conversations)
            {
                var satisfied = conv.Satisfied ? "Yes" : "No";
                sb.AppendLine($"| {conv.Date:yyyy-MM-dd} | {conv.Topic ?? "-"} | {conv.TurnCount} | {satisfied} |");
            }

            return sb.ToString();
        }, "Error fetching user journey");
}

#region DTOs

internal sealed record ConversationListDto(
    [property: JsonPropertyName("conversations")]
    List<ConversationSummaryDto>? Conversations,
    [property: JsonPropertyName("total")] long Total,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("pageSize")]
    int PageSize);

internal sealed record ConversationSummaryDto(
    [property: JsonPropertyName("conversationId")]
    string ConversationId,
    [property: JsonPropertyName("startTime")]
    string StartTime,
    [property: JsonPropertyName("durationMs")]
    double DurationMs,
    [property: JsonPropertyName("turnCount")]
    long TurnCount,
    [property: JsonPropertyName("errorCount")]
    long ErrorCount,
    [property: JsonPropertyName("hasErrors")]
    bool HasErrors,
    [property: JsonPropertyName("totalInputTokens")]
    long TotalInputTokens,
    [property: JsonPropertyName("totalOutputTokens")]
    long TotalOutputTokens,
    [property: JsonPropertyName("userId")] string? UserId,
    [property: JsonPropertyName("firstQuestion")]
    string? FirstQuestion);

internal sealed record ConversationDetailDto(
    [property: JsonPropertyName("conversationId")]
    string ConversationId,
    [property: JsonPropertyName("turns")] List<ConversationTurnDto>? Turns);

internal sealed record ConversationTurnDto(
    [property: JsonPropertyName("spanId")] string SpanId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("timestamp")]
    string Timestamp,
    [property: JsonPropertyName("durationMs")]
    double DurationMs,
    [property: JsonPropertyName("statusCode")]
    byte StatusCode,
    [property: JsonPropertyName("statusMessage")]
    string? StatusMessage,
    [property: JsonPropertyName("provider")]
    string? Provider,
    [property: JsonPropertyName("model")] string? Model,
    [property: JsonPropertyName("inputTokens")]
    long InputTokens,
    [property: JsonPropertyName("outputTokens")]
    long OutputTokens,
    [property: JsonPropertyName("toolName")]
    string? ToolName,
    [property: JsonPropertyName("stopReason")]
    string? StopReason,
    [property: JsonPropertyName("operationName")]
    string? OperationName,
    [property: JsonPropertyName("userId")] string? UserId,
    [property: JsonPropertyName("dataSourceId")]
    string? DataSourceId);

internal sealed record CoverageGapsDto(
    [property: JsonPropertyName("conversationsProcessed")]
    long ConversationsProcessed,
    [property: JsonPropertyName("gapsIdentified")]
    int GapsIdentified,
    [property: JsonPropertyName("gaps")] List<CoverageGapDto>? Gaps);

internal sealed record CoverageGapDto(
    [property: JsonPropertyName("topic")] string Topic,
    [property: JsonPropertyName("conversationCount")]
    long ConversationCount,
    [property: JsonPropertyName("finding")]
    string? Finding,
    [property: JsonPropertyName("recommendation")]
    string? Recommendation,
    [property: JsonPropertyName("sampleConversationIds")]
    List<string>? SampleConversationIds);

internal sealed record TopQuestionsDto(
    [property: JsonPropertyName("conversationsProcessed")]
    long ConversationsProcessed,
    [property: JsonPropertyName("clustersIdentified")]
    int ClustersIdentified,
    [property: JsonPropertyName("clusters")]
    List<TopQuestionClusterDto>? Clusters);

internal sealed record TopQuestionClusterDto(
    [property: JsonPropertyName("topic")] string Topic,
    [property: JsonPropertyName("conversationCount")]
    long ConversationCount,
    [property: JsonPropertyName("sampleConversationIds")]
    List<string>? SampleConversationIds);

internal sealed record SourceAnalyticsDto(
    [property: JsonPropertyName("sources")]
    List<SourceUsageDto>? Sources);

internal sealed record SourceUsageDto(
    [property: JsonPropertyName("sourceId")]
    string SourceId,
    [property: JsonPropertyName("citationCount")]
    long CitationCount,
    [property: JsonPropertyName("topQuestions")]
    List<string>? TopQuestions);

internal sealed record SatisfactionDto(
    [property: JsonPropertyName("totalFeedback")]
    long TotalFeedback,
    [property: JsonPropertyName("upvotes")]
    long Upvotes,
    [property: JsonPropertyName("downvotes")]
    long Downvotes,
    [property: JsonPropertyName("satisfactionRate")]
    double SatisfactionRate,
    [property: JsonPropertyName("byModel")]
    List<SatisfactionByModelDto>? ByModel,
    [property: JsonPropertyName("byTopic")]
    List<SatisfactionByTopicDto>? ByTopic);

internal sealed record SatisfactionByModelDto(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("rate")] double Rate,
    [property: JsonPropertyName("downvotes")]
    long Downvotes);

internal sealed record SatisfactionByTopicDto(
    [property: JsonPropertyName("topic")] string Topic,
    [property: JsonPropertyName("rate")] double Rate,
    [property: JsonPropertyName("downvotes")]
    long Downvotes);

internal sealed record UserListDto(
    [property: JsonPropertyName("users")] List<UserSummaryDto>? Users,
    [property: JsonPropertyName("total")] long Total);

internal sealed record UserSummaryDto(
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("conversationCount")]
    long ConversationCount,
    [property: JsonPropertyName("firstSeen")]
    string FirstSeen,
    [property: JsonPropertyName("lastSeen")]
    string LastSeen,
    [property: JsonPropertyName("topTopics")]
    List<string>? TopTopics);

internal sealed record UserJourneyDto(
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("conversations")]
    List<UserConversationDto>? Conversations,
    [property: JsonPropertyName("totalTokens")]
    long TotalTokens,
    [property: JsonPropertyName("frequentTopics")]
    List<string>? FrequentTopics,
    [property: JsonPropertyName("retentionDays")]
    int RetentionDays);

internal sealed record UserConversationDto(
    [property: JsonPropertyName("conversationId")]
    string ConversationId,
    [property: JsonPropertyName("date")] string Date,
    [property: JsonPropertyName("topic")] string? Topic,
    [property: JsonPropertyName("turnCount")]
    long TurnCount,
    [property: JsonPropertyName("satisfied")]
    bool Satisfied);

#endregion

[JsonSerializable(typeof(ConversationListDto))]
[JsonSerializable(typeof(ConversationDetailDto))]
[JsonSerializable(typeof(CoverageGapsDto))]
[JsonSerializable(typeof(TopQuestionsDto))]
[JsonSerializable(typeof(SourceAnalyticsDto))]
[JsonSerializable(typeof(SatisfactionDto))]
[JsonSerializable(typeof(UserListDto))]
[JsonSerializable(typeof(UserJourneyDto))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class AnalyticsJsonContext : JsonSerializerContext;
