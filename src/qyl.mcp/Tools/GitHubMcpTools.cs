using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tools for GitHub integration: trigger code reviews,
///     get review results, and list webhook events.
/// </summary>
[McpServerToolType]
internal sealed class GitHubMcpTools(HttpClient http)
{
    [McpServerTool(Name = "qyl.trigger_code_review", Title = "Trigger Code Review",
        ReadOnly = false, Destructive = false, Idempotent = true)]
    [Description("""
                 Trigger an AI-powered code review for a GitHub pull request.
                 Fetches the PR diff, analyzes it with an LLM, and returns
                 structured review comments with severity, file, line, and suggestions.
                 """)]
    public async Task<string> TriggerCodeReviewAsync(
        [Description("GitHub repo full name (e.g. 'owner/repo')")] string repoFullName,
        [Description("Pull request number")] int prNumber,
        CancellationToken ct = default) =>
        await CollectorHelper.ExecuteAsync(async () =>
        {
            using HttpResponseMessage resp = await http
                .PostAsync(
                    $"/api/v1/code-review/{Uri.EscapeDataString(repoFullName)}/pulls/{prNumber}",
                    null, ct)
                .ConfigureAwait(false);

            resp.EnsureSuccessStatusCode();
            string json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return FormatCodeReview(json, repoFullName, prNumber);
        });

    [McpServerTool(Name = "qyl.get_code_review", Title = "Get Code Review Results",
        ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("""
                 Get cached code review results for a GitHub pull request.
                 Returns the review comments from the most recent analysis.
                 Use trigger_code_review first to generate a review.
                 """)]
    public async Task<string> GetCodeReviewAsync(
        [Description("GitHub repo full name (e.g. 'owner/repo')")] string repoFullName,
        [Description("Pull request number")] int prNumber,
        CancellationToken ct = default) =>
        await CollectorHelper.ExecuteAsync(async () =>
        {
            using HttpResponseMessage resp = await http
                .GetAsync(
                    $"/api/v1/code-review/{Uri.EscapeDataString(repoFullName)}/pulls/{prNumber}", ct)
                .ConfigureAwait(false);

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                return $"No code review found for {repoFullName} PR #{prNumber}. Trigger a review first.";

            resp.EnsureSuccessStatusCode();
            string json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return FormatCodeReview(json, repoFullName, prNumber);
        });

    [McpServerTool(Name = "qyl.list_github_events", Title = "List GitHub Webhook Events",
        ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("""
                 List recent GitHub webhook events received by the platform.
                 Shows push, pull_request, deployment, and other events.
                 Optionally filter by event type or repository.
                 """)]
    public async Task<string> ListGitHubEventsAsync(
        [Description("Maximum number of events to return (default: 20)")] int? limit = null,
        [Description("Filter by event type (e.g. 'push', 'pull_request')")] string? eventType = null,
        [Description("Filter by repository full name")] string? repoFullName = null,
        CancellationToken ct = default) =>
        await CollectorHelper.ExecuteAsync(async () =>
        {
            int take = Math.Clamp(limit ?? 20, 1, 100);
            string url = $"/api/v1/github/events?limit={take}";
            if (eventType is not null)
                url += $"&eventType={Uri.EscapeDataString(eventType)}";
            if (repoFullName is not null)
                url += $"&repoFullName={Uri.EscapeDataString(repoFullName)}";

            using HttpResponseMessage resp = await http.GetAsync(url, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            string json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return FormatEventList(json);
        });

    private static string FormatCodeReview(string json, string repoFullName, int prNumber)
    {
        try
        {
            JsonNode? root = JsonNode.Parse(json);
            bool reviewed = root?["reviewed"]?.GetValue<bool>() ?? false;
            if (!reviewed)
                return $"Code review for {repoFullName} PR #{prNumber} was not completed (no LLM configured or PR not accessible).";

            JsonArray? comments = root?["comments"]?.AsArray();
            if (comments is null || comments.Count == 0)
                return $"Code review for {repoFullName} PR #{prNumber}: no issues found.";

            StringBuilder sb = new();
            sb.AppendLine($"## Code Review: {repoFullName} PR #{prNumber}");
            sb.AppendLine($"**{comments.Count}** issue(s) found:");
            sb.AppendLine();
            foreach (JsonNode? c in comments)
            {
                if (c is null) continue;
                string severity = c["severity"]?.ToString() ?? "?";
                string file = c["file"]?.ToString() ?? "?";
                string line = c["line"]?.ToString() ?? "?";
                string comment = c["comment"]?.ToString() ?? "";
                sb.AppendLine($"- **[{severity}]** `{file}:{line}` — {comment}");
            }

            return sb.ToString();
        }
        catch (System.Text.Json.JsonException)
        {
            return json;
        }
    }

    private static string FormatEventList(string json)
    {
        try
        {
            JsonNode? root = JsonNode.Parse(json);
            JsonArray? items = root?["items"]?.AsArray();
            if (items is null || items.Count == 0)
                return "No GitHub webhook events found.";

            StringBuilder sb = new();
            sb.AppendLine("## GitHub Webhook Events");
            sb.AppendLine();
            foreach (JsonNode? item in items)
            {
                if (item is null) continue;
                string type = item["eventType"]?.ToString() ?? "?";
                string action = item["action"]?.ToString() ?? "";
                string repo = item["repoFullName"]?.ToString() ?? "?";
                string time = item["createdAt"]?.ToString() ?? "?";
                string display = string.IsNullOrEmpty(action) ? type : $"{type}.{action}";
                sb.AppendLine($"- **{display}** | {repo} | {time}");
            }

            return sb.ToString();
        }
        catch (System.Text.Json.JsonException)
        {
            return json;
        }
    }
}
