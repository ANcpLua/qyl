using System.ComponentModel;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tools for regression detection: trigger checks and query regression events.
/// </summary>
[McpServerToolType]
internal sealed class RegressionTools(HttpClient http)
{
    [McpServerTool(Name = "qyl.check_regressions", Title = "Check Regressions for Service",
        ReadOnly = false, Destructive = false, Idempotent = true)]
    [Description("""
                 Trigger a regression check for a specific service.
                 Detects resolved errors that have re-appeared and transitions
                 them to 'regressed' status. Optionally scope to a specific deploy version.
                 """)]
    public async Task<string> CheckRegressionsAsync(
        [Description("The service name to check for regressions")] string serviceName,
        [Description("Optional deploy version to scope the check")] string? version = null,
        CancellationToken ct = default) =>
        await CollectorHelper.ExecuteAsync(async () =>
        {
            string url = $"/api/v1/regressions/check/{Uri.EscapeDataString(serviceName)}";
            if (version is not null)
                url += $"?version={Uri.EscapeDataString(version)}";

            using HttpResponseMessage resp = await http
                .PostAsync(url, null, ct)
                .ConfigureAwait(false);

            resp.EnsureSuccessStatusCode();
            string json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            JsonNode? root = JsonNode.Parse(json);
            int count = root?["count"]?.GetValue<int>() ?? 0;
            if (count == 0)
                return $"No regressions detected for service '{serviceName}'.";

            JsonArray? ids = root?["regressedIssueIds"]?.AsArray();
            StringBuilder sb = new();
            sb.AppendLine($"## Regressions Detected for {serviceName}");
            sb.AppendLine($"**{count}** resolved issue(s) regressed:");
            sb.AppendLine();
            if (ids is not null)
            {
                foreach (JsonNode? id in ids)
                    sb.AppendLine($"- {id}");
            }

            return sb.ToString();
        });

    [McpServerTool(Name = "qyl.list_regressions", Title = "List Regression Events",
        ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("""
                 List recent regression events across all issues.
                 Shows which issues regressed, when, and why.
                 Optionally filter by time range.
                 """)]
    public async Task<string> ListRegressionsAsync(
        [Description("Maximum number of events to return (default: 20)")] int? limit = null,
        [Description("Only show regressions after this ISO timestamp")] string? since = null,
        CancellationToken ct = default) =>
        await CollectorHelper.ExecuteAsync(async () =>
        {
            int take = Math.Clamp(limit ?? 20, 1, 100);
            string url = $"/api/v1/regressions?limit={take}";
            if (since is not null)
                url += $"&since={Uri.EscapeDataString(since)}";

            using HttpResponseMessage resp = await http
                .GetAsync(url, ct)
                .ConfigureAwait(false);

            resp.EnsureSuccessStatusCode();
            string json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return FormatRegressionList(json);
        });

    [McpServerTool(Name = "qyl.get_issue_regressions", Title = "Get Issue Regression History",
        ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("""
                 Get the regression history for a specific issue.
                 Shows all times this issue has regressed after being resolved.
                 """)]
    public async Task<string> GetIssueRegressionsAsync(
        [Description("The error issue ID")] string issueId,
        [Description("Maximum number of events to return (default: 10)")] int? limit = null,
        CancellationToken ct = default) =>
        await CollectorHelper.ExecuteAsync(async () =>
        {
            int take = Math.Clamp(limit ?? 10, 1, 100);
            using HttpResponseMessage resp = await http
                .GetAsync($"/api/v1/issues/{Uri.EscapeDataString(issueId)}/regressions?limit={take}", ct)
                .ConfigureAwait(false);

            resp.EnsureSuccessStatusCode();
            string json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return FormatRegressionList(json, issueId);
        });

    private static string FormatRegressionList(string json, string? issueId = null)
    {
        try
        {
            JsonNode? root = JsonNode.Parse(json);
            JsonArray? items = root?["items"]?.AsArray();
            if (items is null || items.Count == 0)
                return issueId is not null
                    ? $"No regressions found for issue '{issueId}'."
                    : "No regression events found.";

            StringBuilder sb = new();
            sb.AppendLine(issueId is not null
                ? $"## Regressions for Issue {issueId}"
                : "## Recent Regressions");
            sb.AppendLine();
            foreach (JsonNode? item in items)
            {
                if (item is null) continue;
                string id = item["issueId"]?.ToString() ?? "?";
                string reason = item["reason"]?.ToString() ?? "unknown";
                string time = item["createdAt"]?.ToString() ?? "?";
                sb.AppendLine($"- **{id}** | {reason} | {time}");
            }

            return sb.ToString();
        }
        catch (System.Text.Json.JsonException)
        {
            return json;
        }
    }
}
