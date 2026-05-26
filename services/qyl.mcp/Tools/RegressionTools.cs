using System.Text.Json;
using System.Text.Json.Nodes;
using ANcpLua.Roslyn.Utilities.Web;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

[McpServerToolType]
[QylSkill(QylSkillKind.Loom)]
internal sealed partial class RegressionTools(HttpClient http)
{
    [QylCapability("anomaly_detection", QylCapabilityRole.FollowUp)]
    [QylCapability("loom_triage_and_fix")]
    [McpServerTool(Name = "qyl.check_regressions", Title = "Check Regressions for Service",
        ReadOnly = true, Destructive = false, Idempotent = true,
        TaskSupport = ToolTaskSupport.Optional)]
    public async partial Task<string> CheckRegressionsAsync(
        string serviceName,
        string? version = null,
        CancellationToken ct = default) =>
        await CollectorHelper.ExecuteAsync(async () =>
        {
            var url = QueryString.AppendPairs(
                $"/api/v1/regressions/check/{Uri.EscapeDataString(serviceName)}",
                ("version", version));

            using var resp = await http
                .PostAsync(url, null, ct)
                .ConfigureAwait(false);

            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            var root = JsonNode.Parse(json);
            var count = root?["count"]?.GetValue<int>() ?? 0;
            if (count is 0)
                return $"No regressions detected for service '{serviceName}'.";

            var ids = root?["regressedIssueIds"]?.AsArray();
            StringBuilder sb = new();
            sb.AppendLine($"## Regressions Detected for {serviceName}");
            sb.AppendLine($"**{count}** resolved issue(s) regressed:");
            sb.AppendLine();
            if (ids is not null)
            {
                foreach (var id in ids)
                    sb.AppendLine($"- {id}");
            }

            return sb.ToString();
        });

    [McpServerTool(Name = "qyl.list_regressions", Title = "List Regression Events",
        ReadOnly = true, Destructive = false, Idempotent = true,
        TaskSupport = ToolTaskSupport.Optional)]
    public async partial Task<string> ListRegressionsAsync(
        int? limit = null,
        string? since = null,
        CancellationToken ct = default) =>
        await CollectorHelper.ExecuteAsync(async () =>
        {
            var take = Math.Clamp(limit ?? 20, 1, 100);
            var url = QueryString.AppendPairs(
                $"/api/v1/regressions?limit={take}", ("since", since));

            using var resp = await http
                .GetAsync(url, ct)
                .ConfigureAwait(false);

            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return FormatRegressionList(json);
        });

    [McpServerTool(Name = "qyl.get_issue_regressions", Title = "Get Issue Regression History",
        ReadOnly = true, Destructive = false, Idempotent = true)]
    public async partial Task<string> GetIssueRegressionsAsync(
        string issueId,
        int? limit = null,
        CancellationToken ct = default) =>
        await CollectorHelper.ExecuteAsync(async () =>
        {
            var take = Math.Clamp(limit ?? 10, 1, 100);
            using var resp = await http
                .GetAsync($"/api/v1/issues/{Uri.EscapeDataString(issueId)}/regressions?limit={take}", ct)
                .ConfigureAwait(false);

            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return FormatRegressionList(json, issueId);
        });

    private static string FormatRegressionList(string json, string? issueId = null)
    {
        try
        {
            var root = JsonNode.Parse(json);
            var items = root?["items"]?.AsArray();
            if (items is null || items.Count is 0)
            {
                return issueId is not null
                    ? $"No regressions found for issue '{issueId}'."
                    : "No regression events found.";
            }

            StringBuilder sb = new();
            sb.AppendLine(issueId is not null
                ? $"## Regressions for Issue {issueId}"
                : "## Recent Regressions");
            sb.AppendLine();
            foreach (var item in items)
            {
                if (item is null) continue;
                var id = item["issueId"]?.ToString() ?? "?";
                var reason = item["reason"]?.ToString() ?? "unknown";
                var time = item["createdAt"]?.ToString() ?? "?";
                sb.AppendLine($"- **{id}** | {reason} | {time}");
            }

            return sb.ToString();
        }
        catch (JsonException)
        {
            return json;
        }
    }
}
