using System.Net.Http.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using qyl.mcp.Agents;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tool for natural language → DuckDB SQL → formatted results.
///     Uses an LLM to translate questions into SQL, executes against
///     the collector's /api/v1/query endpoint, and formats output.
/// </summary>
[McpServerToolType]
[QylSkill(QylSkillKind.Loom)]
internal sealed partial class AssistedQueryTools(HttpClient http, IQylMcpAgentsBuilder agents)
{

    [McpServerTool(Name = "qyl.assisted_query", Title = "Assisted Query",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true,
        TaskSupport = ToolTaskSupport.Optional)]
    public async partial Task<string> AssistedQueryAsync(
        string question,
        int? limit = null,
        CancellationToken ct = default) =>
        await CollectorHelper.ExecuteAsync(async () =>
        {
            if (!agents.IsConfigured)
                return "Assisted query requires an LLM. Set QYL_AGENT_API_KEY to enable.";

            var take = Math.Clamp(limit ?? 50, 1, 500);

            var agent = agents.BuildAssistedQueryAgent(take);

            var response = await agent.RunAsync(question, cancellationToken: ct).ConfigureAwait(false);

            var sql = ExtractSql(response.Text ?? string.Empty);
            if (string.IsNullOrWhiteSpace(sql))
                return "Could not generate a valid SQL query from your question. Try rephrasing.";

            using var resp = await http.PostAsJsonAsync(
                "/api/v1/query",
                new { sql, limit = take },
                ct).ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
            {
                var error = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                return $"Query failed: {error}\n\nGenerated SQL:\n```sql\n{sql}\n```";
            }

            var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return $"## Query Results\n\n**SQL:**\n```sql\n{sql}\n```\n\n**Results:**\n```json\n{json}\n```";
        });

    private static string ExtractSql(string text)
    {
        var start = text.IndexOfIgnoreCase("```sql");
        if (start >= 0)
        {
            start = text.IndexOf('\n', start) + 1;
            var end = text.IndexOf("```", start, StringComparison.Ordinal);
            if (end > start)
                return text[start..end].Trim();
        }

        // Fallback: try to find a SELECT statement
        var selectIdx = text.IndexOfIgnoreCase("SELECT");
        return selectIdx >= 0
            ? text[selectIdx..].Trim().TrimEnd(';', '`', '\n', '\r')
            : "";
    }
}
