using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tools for coding agent handoff: discover pending work,
///     accept handoffs, get context, submit results, and report failures.
///     Designed for external agents (Claude Code, Cursor, Copilot) to
///     consume fix run context and submit completed fixes.
/// </summary>
[McpServerToolType]
internal sealed class AgentHandoffTools(HttpClient http)
{
    [McpServerTool(Name = "qyl.get_pending_handoffs", Title = "Get Pending Handoffs",
        ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("""
                 List pending agent handoffs waiting to be claimed.
                 Each handoff represents a fix run that needs an external
                 coding agent to implement the solution.
                 """)]
    public async Task<string> GetPendingHandoffsAsync(
        [Description("Maximum number of handoffs to return (default: 10)")] int? limit = null,
        CancellationToken ct = default) =>
        await CollectorHelper.ExecuteAsync(async () =>
        {
            int take = Math.Clamp(limit ?? 10, 1, 100);
            using HttpResponseMessage resp = await http
                .GetAsync($"/api/v1/handoffs/pending?limit={take}", ct)
                .ConfigureAwait(false);

            resp.EnsureSuccessStatusCode();
            string json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return FormatHandoffList(json, "Pending Handoffs");
        });

    [McpServerTool(Name = "qyl.get_handoff_context", Title = "Get Handoff Context",
        ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("""
                 Get the full context for a handoff — includes root cause analysis,
                 solution plan, generated diff, and all autofix pipeline step outputs.
                 Use this to understand what fix needs to be implemented.
                 """)]
    public async Task<string> GetHandoffContextAsync(
        [Description("The handoff ID")] string handoffId,
        CancellationToken ct = default) =>
        await CollectorHelper.ExecuteAsync(async () =>
        {
            using HttpResponseMessage resp = await http
                .GetAsync($"/api/v1/handoffs/{Uri.EscapeDataString(handoffId)}/context", ct)
                .ConfigureAwait(false);

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                return $"Handoff '{handoffId}' not found.";

            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        });

    [McpServerTool(Name = "qyl.accept_handoff", Title = "Accept Handoff",
        ReadOnly = false, Destructive = false, Idempotent = true)]
    [Description("""
                 Accept a pending handoff, claiming it for this agent.
                 The handoff transitions from 'pending' to 'accepted'.
                 After accepting, implement the fix and submit the result.
                 """)]
    public async Task<string> AcceptHandoffAsync(
        [Description("The handoff ID to accept")] string handoffId,
        CancellationToken ct = default) =>
        await CollectorHelper.ExecuteAsync(async () =>
        {
            using HttpResponseMessage resp = await http
                .PostAsync($"/api/v1/handoffs/{Uri.EscapeDataString(handoffId)}/accept", null, ct)
                .ConfigureAwait(false);

            if (resp.StatusCode == System.Net.HttpStatusCode.Conflict)
                return $"Handoff '{handoffId}' is not in 'pending' status or was already claimed.";

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                return $"Handoff '{handoffId}' not found.";

            resp.EnsureSuccessStatusCode();
            return $"Handoff '{handoffId}' accepted. Implement the fix and call qyl.submit_agent_fix when done.";
        });

    [McpServerTool(Name = "qyl.submit_agent_fix", Title = "Submit Agent Fix",
        ReadOnly = false, Destructive = false, Idempotent = false)]
    [Description("""
                 Submit the result of a completed fix for an accepted handoff.
                 Provide a JSON object describing the changes made (file paths,
                 diffs, PR URL if applicable).
                 """)]
    public async Task<string> SubmitAgentFixAsync(
        [Description("The handoff ID")] string handoffId,
        [Description("JSON describing the fix result (files changed, diffs, PR URL, etc.)")] string resultJson,
        CancellationToken ct = default) =>
        await CollectorHelper.ExecuteAsync(async () =>
        {
            using HttpResponseMessage resp = await http.PostAsJsonAsync(
                $"/api/v1/handoffs/{Uri.EscapeDataString(handoffId)}/submit",
                new { resultJson },
                ct).ConfigureAwait(false);

            if (resp.StatusCode == System.Net.HttpStatusCode.Conflict)
                return $"Handoff '{handoffId}' is not in 'accepted' status.";

            resp.EnsureSuccessStatusCode();
            return $"Fix submitted for handoff '{handoffId}'. The fix run will be updated.";
        });

    [McpServerTool(Name = "qyl.fail_handoff", Title = "Report Handoff Failure",
        ReadOnly = false, Destructive = false, Idempotent = true)]
    [Description("""
                 Report that a handoff could not be completed.
                 Provide a reason for the failure. The handoff transitions to 'failed'.
                 """)]
    public async Task<string> FailHandoffAsync(
        [Description("The handoff ID")] string handoffId,
        [Description("Reason the fix could not be completed")] string errorMessage,
        CancellationToken ct = default) =>
        await CollectorHelper.ExecuteAsync(async () =>
        {
            using HttpResponseMessage resp = await http.PostAsJsonAsync(
                $"/api/v1/handoffs/{Uri.EscapeDataString(handoffId)}/fail",
                new { errorMessage },
                ct).ConfigureAwait(false);

            if (resp.StatusCode == System.Net.HttpStatusCode.Conflict)
                return $"Handoff '{handoffId}' is not in a claimable status.";

            resp.EnsureSuccessStatusCode();
            return $"Handoff '{handoffId}' marked as failed.";
        });

    private static string FormatHandoffList(string json, string title)
    {
        try
        {
            JsonNode? root = JsonNode.Parse(json);
            JsonArray? items = root?["items"]?.AsArray();
            if (items is null || items.Count == 0)
                return "No pending handoffs available.";

            StringBuilder sb = new();
            sb.AppendLine($"## {title}");
            sb.AppendLine();
            foreach (JsonNode? item in items)
            {
                if (item is null) continue;
                string id = item["handoffId"]?.ToString() ?? "?";
                string runId = item["runId"]?.ToString() ?? "?";
                string agent = item["agentType"]?.ToString() ?? "?";
                string status = item["status"]?.ToString() ?? "?";
                string time = item["createdAt"]?.ToString() ?? "?";
                sb.AppendLine($"- **{id}** | run={runId} | agent={agent} | status={status} | {time}");
            }

            return sb.ToString();
        }
        catch (System.Text.Json.JsonException)
        {
            return json;
        }
    }
}
