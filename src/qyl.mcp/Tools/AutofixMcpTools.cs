using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tools for managing autofix fix runs: trigger, list, get details,
///     view pipeline steps, approve, and reject.
/// </summary>
[McpServerToolType]
internal sealed class AutofixMcpTools(HttpClient http)
{
    [McpServerTool(Name = "qyl.list_fix_runs", Title = "List Fix Runs",
        ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("""
                 List fix runs for an error issue, ordered by most recent first.
                 Returns run IDs, statuses, confidence scores, and timestamps.
                 """)]
    public async Task<string> ListFixRunsAsync(
        [Description("The error issue ID")] string issueId,
        [Description("Maximum number of runs to return (default: 10)")] int? limit = null,
        CancellationToken ct = default) =>
        await CollectorHelper.ExecuteAsync(async () =>
        {
            int take = Math.Clamp(limit ?? 10, 1, 100);
            using HttpResponseMessage resp = await http
                .GetAsync($"/api/v1/issues/{Uri.EscapeDataString(issueId)}/fix-runs?limit={take}", ct)
                .ConfigureAwait(false);

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                return $"Issue '{issueId}' not found.";

            resp.EnsureSuccessStatusCode();
            string json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return FormatFixRunList(json, issueId);
        });

    [McpServerTool(Name = "qyl.get_fix_run", Title = "Get Fix Run Details",
        ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("""
                 Get full details of a specific fix run including the generated
                 changes JSON, confidence score, and pipeline status.
                 """)]
    public async Task<string> GetFixRunAsync(
        [Description("The error issue ID")] string issueId,
        [Description("The fix run ID")] string runId,
        CancellationToken ct = default) =>
        await CollectorHelper.ExecuteAsync(async () =>
        {
            using HttpResponseMessage resp = await http
                .GetAsync($"/api/v1/issues/{Uri.EscapeDataString(issueId)}/fix-runs/{Uri.EscapeDataString(runId)}", ct)
                .ConfigureAwait(false);

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                return $"Fix run '{runId}' not found for issue '{issueId}'.";

            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        });

    [McpServerTool(Name = "qyl.get_fix_run_steps", Title = "Get Fix Run Pipeline Steps",
        ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("""
                 Get the individual pipeline steps for a fix run.
                 Shows status, timing, and output for each step:
                 gather_context → root_cause_analysis → solution_planning →
                 diff_generation → confidence_scoring.
                 """)]
    public async Task<string> GetFixRunStepsAsync(
        [Description("The error issue ID")] string issueId,
        [Description("The fix run ID")] string runId,
        CancellationToken ct = default) =>
        await CollectorHelper.ExecuteAsync(async () =>
        {
            using HttpResponseMessage resp = await http
                .GetAsync($"/api/v1/issues/{Uri.EscapeDataString(issueId)}/fix-runs/{Uri.EscapeDataString(runId)}/steps", ct)
                .ConfigureAwait(false);

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                return $"Fix run '{runId}' not found for issue '{issueId}'.";

            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        });

    [McpServerTool(Name = "qyl.approve_fix_run", Title = "Approve Fix Run",
        ReadOnly = false, Destructive = false, Idempotent = true)]
    [Description("""
                 Approve a fix run that is in 'review' status.
                 This transitions the fix run to 'applied' status,
                 allowing it to be used for PR creation or coding agent handoff.
                 """)]
    public async Task<string> ApproveFixRunAsync(
        [Description("The error issue ID")] string issueId,
        [Description("The fix run ID to approve")] string runId,
        CancellationToken ct = default) =>
        await CollectorHelper.ExecuteAsync(async () =>
        {
            using HttpResponseMessage resp = await http
                .PostAsync(
                    $"/api/v1/issues/{Uri.EscapeDataString(issueId)}/fix-runs/{Uri.EscapeDataString(runId)}/approve",
                    null, ct)
                .ConfigureAwait(false);

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                return $"Fix run '{runId}' not found for issue '{issueId}'.";

            if (resp.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                string error = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                return $"Cannot approve: {error}";
            }

            resp.EnsureSuccessStatusCode();
            return $"Fix run '{runId}' approved and moved to 'applied' status.";
        });

    [McpServerTool(Name = "qyl.reject_fix_run", Title = "Reject Fix Run",
        ReadOnly = false, Destructive = false, Idempotent = true)]
    [Description("""
                 Reject a fix run that is in 'review' status.
                 Optionally provide a reason for rejection.
                 """)]
    public async Task<string> RejectFixRunAsync(
        [Description("The error issue ID")] string issueId,
        [Description("The fix run ID to reject")] string runId,
        [Description("Optional reason for rejection")] string? reason = null,
        CancellationToken ct = default) =>
        await CollectorHelper.ExecuteAsync(async () =>
        {
            using HttpResponseMessage resp = await http.PostAsJsonAsync(
                $"/api/v1/issues/{Uri.EscapeDataString(issueId)}/fix-runs/{Uri.EscapeDataString(runId)}/reject",
                new { reason },
                ct).ConfigureAwait(false);

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                return $"Fix run '{runId}' not found for issue '{issueId}'.";

            if (resp.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                string error = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                return $"Cannot reject: {error}";
            }

            resp.EnsureSuccessStatusCode();
            return $"Fix run '{runId}' rejected.{(reason is not null ? $" Reason: {reason}" : "")}";
        });

    private static string FormatFixRunList(string json, string issueId)
    {
        try
        {
            JsonNode? root = JsonNode.Parse(json);
            JsonArray? items = root?["items"]?.AsArray();
            if (items is null || items.Count == 0)
                return $"No fix runs found for issue '{issueId}'.";

            StringBuilder sb = new();
            sb.AppendLine($"## Fix Runs for {issueId}");
            sb.AppendLine();
            foreach (JsonNode? item in items)
            {
                if (item is null) continue;
                sb.AppendLine($"- **{item["runId"]}** | status={item["status"]} | confidence={item["confidenceScore"]} | created={item["createdAt"]}");
            }

            return sb.ToString();
        }
        catch (System.Text.Json.JsonException)
        {
            return json;
        }
    }
}
