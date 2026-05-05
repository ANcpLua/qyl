using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using ANcpLua.Roslyn.Utilities.Web;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

[McpServerToolType]
[QylSkill(QylSkillKind.Loom)]
internal sealed partial class GitHubMcpTools(HttpClient http)
{
    [QylCapability("loom_triage_and_fix", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "qyl.trigger_code_review", Title = "Trigger Code Review",
        ReadOnly = false, Destructive = false, Idempotent = true,
        TaskSupport = ToolTaskSupport.Optional)]
    public async partial Task<string> TriggerCodeReviewAsync(
        string repoFullName,
        int prNumber,
        CancellationToken ct = default) =>
        await CollectorHelper.ExecuteAsync(async () =>
        {
            if (!TrySplitRepoFullName(repoFullName, out var owner, out var repo))
                return $"Invalid repo full name '{repoFullName}'. Expected 'owner/repo'.";

            using var resp = await http
                .PostAsync(BuildCodeReviewPath(owner, repo, prNumber), null, ct)
                .ConfigureAwait(false);

            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return FormatCodeReview(json, repoFullName, prNumber);
        });

    [McpServerTool(Name = "qyl.get_code_review", Title = "Get Code Review Results",
        ReadOnly = true, Destructive = false, Idempotent = true)]
    public async partial Task<string> GetCodeReviewAsync(
        string repoFullName,
        int prNumber,
        CancellationToken ct = default) =>
        await CollectorHelper.ExecuteAsync(async () =>
        {
            if (!TrySplitRepoFullName(repoFullName, out var owner, out var repo))
                return $"Invalid repo full name '{repoFullName}'. Expected 'owner/repo'.";

            using var resp = await http
                .GetAsync(BuildCodeReviewPath(owner, repo, prNumber), ct)
                .ConfigureAwait(false);

            if (resp.StatusCode == HttpStatusCode.NotFound)
                return $"No code review found for {repoFullName} PR #{prNumber}. Trigger a review first.";

            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return FormatCodeReview(json, repoFullName, prNumber);
        });

    [McpServerTool(Name = "qyl.list_github_events", Title = "List GitHub Webhook Events",
        ReadOnly = true, Destructive = false, Idempotent = true,
        TaskSupport = ToolTaskSupport.Optional)]
    public async partial Task<string> ListGitHubEventsAsync(
        int? limit = null,
        string? eventType = null,
        string? repoFullName = null,
        CancellationToken ct = default) =>
        await CollectorHelper.ExecuteAsync(async () =>
        {
            var take = Math.Clamp(limit ?? 20, 1, 100);
            var url = QueryString.AppendPairs(
                $"/api/v1/github/events?limit={take}",
                ("eventType", eventType), ("repoFullName", repoFullName));

            using var resp = await http.GetAsync(new Uri(url, UriKind.Relative), ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return FormatEventList(json);
        });

    private static string BuildCodeReviewPath(string owner, string repo, int prNumber) =>
        $"/api/v1/code-review/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}/pulls/{prNumber}";

    private static bool TrySplitRepoFullName(string repoFullName, out string owner, out string repo)
    {
        owner = string.Empty;
        repo = string.Empty;

        if (string.IsNullOrWhiteSpace(repoFullName))
            return false;

        var parts = repoFullName.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is not 2)
            return false;

        owner = parts[0];
        repo = parts[1];
        return true;
    }

    private static string FormatCodeReview(string json, string repoFullName, int prNumber)
    {
        try
        {
            var root = JsonNode.Parse(json);
            var reviewed = root?["reviewed"]?.GetValue<bool>() ?? false;
            if (!reviewed)
            {
                return
                    $"Code review for {repoFullName} PR #{prNumber} was not completed (no LLM configured or PR not accessible).";
            }

            var comments = root?["comments"]?.AsArray();
            if (comments is null || comments.Count is 0)
                return $"Code review for {repoFullName} PR #{prNumber}: no issues found.";

            StringBuilder sb = new();
            sb.AppendLine($"## Code Review: {repoFullName} PR #{prNumber}");
            sb.AppendLine($"**{comments.Count}** issue(s) found:");
            sb.AppendLine();
            foreach (var c in comments)
            {
                if (c is null) continue;
                var severity = c["severity"]?.ToString() ?? "?";
                var file = c["file"]?.ToString() ?? "?";
                var line = c["line"]?.ToString() ?? "?";
                var comment = c["comment"]?.ToString() ?? "";
                sb.AppendLine($"- **[{severity}]** `{file}:{line}` — {comment}");
            }

            return sb.ToString();
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private static string FormatEventList(string json)
    {
        try
        {
            var root = JsonNode.Parse(json);
            var items = root?["items"]?.AsArray();
            if (items is null || items.Count is 0)
                return "No GitHub webhook events found.";

            StringBuilder sb = new();
            sb.AppendLine("## GitHub Webhook Events");
            sb.AppendLine();
            foreach (var item in items)
            {
                if (item is null) continue;
                var type = item["eventType"]?.ToString() ?? "?";
                var action = item["action"]?.ToString() ?? "";
                var repo = item["repoFullName"]?.ToString() ?? "?";
                var time = item["createdAt"]?.ToString() ?? "?";
                var display = string.IsNullOrEmpty(action) ? type : $"{type}.{action}";
                sb.AppendLine($"- **{display}** | {repo} | {time}");
            }

            return sb.ToString();
        }
        catch (JsonException)
        {
            return json;
        }
    }
}
