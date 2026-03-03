using System.ComponentModel;
using System.Net.Http.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Server;
using qyl.mcp.Agents;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tool for natural language → DuckDB SQL → formatted results.
///     Uses an LLM to translate questions into SQL, executes against
///     the collector's /api/v1/query endpoint, and formats output.
/// </summary>
[McpServerToolType]
internal sealed class AssistedQueryTools(HttpClient http, IConfiguration config)
{
    private readonly IChatClient? _llm = AgentLlmFactory.TryCreate(config);

    [McpServerTool(Name = "qyl.assisted_query", Title = "Assisted Query",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("""
                 Ask a question about your observability data in natural language.
                 Translates your question to DuckDB SQL, executes it, and returns formatted results.
                 Examples: "What are the top 5 errors by count?", "Show traces slower than 2 seconds",
                 "Which services had errors in the last hour?"
                 """)]
    public async Task<string> AssistedQueryAsync(
        [Description("Your question about the observability data")] string question,
        [Description("Maximum rows to return (default: 50)")] int? limit = null,
        CancellationToken ct = default) =>
        await CollectorHelper.ExecuteAsync(async () =>
        {
            if (_llm is null)
                return "Assisted query requires an LLM. Set QYL_AGENT_API_KEY to enable.";

            int take = Math.Clamp(limit ?? 50, 1, 500);
            string prompt = BuildSqlPrompt(question, take);

            ChatResponse response = await _llm.GetResponseAsync(prompt, cancellationToken: ct)
                .ConfigureAwait(false);

            string sql = ExtractSql(response.Text ?? "");
            if (string.IsNullOrWhiteSpace(sql))
                return "Could not generate a valid SQL query from your question. Try rephrasing.";

            using HttpResponseMessage resp = await http.PostAsJsonAsync(
                "/api/v1/query",
                new { sql, limit = take },
                ct).ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
            {
                string error = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                return $"Query failed: {error}\n\nGenerated SQL:\n```sql\n{sql}\n```";
            }

            string json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return $"## Query Results\n\n**SQL:**\n```sql\n{sql}\n```\n\n**Results:**\n```json\n{json}\n```";
        });

    private static string BuildSqlPrompt(string question, int limit) =>
        $$"""
        You are a DuckDB SQL expert for an observability platform. Generate a single SELECT query.

        ## Available tables (key columns only)
        - spans: trace_id, span_id, service_name, span_name, duration_ms, status, start_time
        - errors: error_id, error_type, error_message, status, fingerprint, first_seen, last_seen, event_count, affected_services
        - logs: log_id, service_name, severity, body, timestamp
        - deployments: deployment_id, service_name, service_version, status, start_time
        - triage_results: triage_id, issue_id, fixability_score, automation_level, ai_summary
        - fix_runs: run_id, issue_id, status, confidence_score, changes_json, created_at
        - issue_events: event_id, issue_id, event_type, old_value, new_value, reason, created_at

        ## Rules
        - DuckDB syntax (supports LIMIT, window functions, array_agg, etc.)
        - SELECT only — no INSERT, UPDATE, DELETE, DDL
        - LIMIT {{limit}} maximum
        - Use now() for current time, interval for time ranges
        - Output ONLY the SQL in a ```sql code block, nothing else

        ## Question
        {{question}}
        """;

    private static string ExtractSql(string text)
    {
        int start = text.IndexOfIgnoreCase("```sql");
        if (start >= 0)
        {
            start = text.IndexOf('\n', start) + 1;
            int end = text.IndexOf("```", start, StringComparison.Ordinal);
            if (end > start)
                return text[start..end].Trim();
        }

        // Fallback: try to find a SELECT statement
        int selectIdx = text.IndexOfIgnoreCase("SELECT");
        if (selectIdx >= 0)
            return text[selectIdx..].Trim().TrimEnd(';', '`', '\n', '\r');

        return "";
    }
}
