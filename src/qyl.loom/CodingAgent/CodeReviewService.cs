using System.Net.Http.Headers;
using Microsoft.Extensions.AI;

namespace Qyl.Loom;

/// <summary>
///     Fetches a PR diff from GitHub, runs LLM-based code review analysis,
///     and posts inline review comments back to the pull request.
/// </summary>
public sealed partial class CodeReviewService(
    GitHubService github,
    DuckDbStore store,
    IHttpClientFactory httpClientFactory,
    ILogger<CodeReviewService> logger,
    IChatClient? llm = null)
{
    private readonly ConcurrentDictionary<string, CodeReviewResult> _cache = new(StringComparer.Ordinal);

    // ── Public API ──────────────────────────────────────────────────────────

    /// <summary>
    ///     Reviews a pull request by fetching its diff, running LLM analysis, and
    ///     returning structured review comments. Results are cached by repo+PR number.
    /// </summary>
    public async Task<CodeReviewResult> ReviewPullRequestAsync(
        string repoFullName, int prNumber, CancellationToken ct = default)
    {
        var cacheKey = $"{repoFullName}#{prNumber}";
        LogReviewStarted(repoFullName, prNumber);

        if (llm is null)
        {
            LogNoLlmConfigured();
            return new CodeReviewResult
            {
                RepoFullName = repoFullName, PrNumber = prNumber, Comments = [], Reviewed = false
            };
        }

        var prData = await FetchPrDiffAsync(repoFullName, prNumber, ct)
            .ConfigureAwait(false);

        if (prData is null)
        {
            return new CodeReviewResult
            {
                RepoFullName = repoFullName, PrNumber = prNumber, Comments = [], Reviewed = false
            };
        }

        // Gather known error patterns from recent issues to feed context to the LLM
        var knownPatterns = await GetKnownErrorPatternsAsync(ct).ConfigureAwait(false);

        var prompt = CodeReviewPrompt.Build(prData.Value.Title, prData.Value.Diff, knownPatterns);

        try
        {
            var response = await llm.GetResponseAsync(prompt, cancellationToken: ct)
                .ConfigureAwait(false);

            var responseText = response.Text ?? "[]";
            var comments = ParseReviewComments(responseText);

            LogReviewComplete(comments.Length);

            CodeReviewResult result = new()
            {
                RepoFullName = repoFullName, PrNumber = prNumber, Comments = comments, Reviewed = true
            };

            _cache[cacheKey] = result;
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogReviewError(ex);
            return new CodeReviewResult
            {
                RepoFullName = repoFullName, PrNumber = prNumber, Comments = [], Reviewed = false
            };
        }
    }

    /// <summary>
    ///     Posts inline review comments to a GitHub pull request.
    ///     Returns <c>true</c> if all comments were posted successfully.
    /// </summary>
    public async Task<bool> PostReviewCommentsAsync(
        string repoFullName, int prNumber,
        IReadOnlyList<CodeReviewComment> comments, CancellationToken ct = default)
    {
        var token = github.GetToken();
        if (token is null || comments.Count == 0)
            return false;

        using var client = CreateGitHubClient(token);
        var allPosted = true;

        foreach (var comment in comments)
        {
            LogPostingComment(comment.File, comment.Line);

            var body = comment.Suggestion is not null
                ? $"**[{comment.Severity}]** {comment.Comment}\n\n```suggestion\n{comment.Suggestion}\n```"
                : $"**[{comment.Severity}]** {comment.Comment}";

            var payload = new CodeReviewCommentPayload(body, comment.File, comment.Line);
            var json = JsonSerializer.Serialize(payload, CodeReviewJsonContext.Default.CodeReviewCommentPayload);
            using StringContent content = new(json, Encoding.UTF8, "application/json");

            using HttpRequestMessage req = new(HttpMethod.Post,
                $"repos/{repoFullName}/pulls/{prNumber}/comments") { Content = content };
            SetAuthHeader(req, token);

            using var response = await client.SendAsync(req, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                allPosted = false;
        }

        return allPosted;
    }

    /// <summary>Returns a cached review result if one exists.</summary>
    public CodeReviewResult? GetCachedResult(string repoFullName, int prNumber)
    {
        var cacheKey = $"{repoFullName}#{prNumber}";
        return _cache.GetValueOrDefault(cacheKey);
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private async Task<(string Title, string Diff)?> FetchPrDiffAsync(
        string repoFullName, int prNumber, CancellationToken ct)
    {
        var token = github.GetToken();
        if (token is null) return null;

        using var client = CreateGitHubClient(token);

        // Get PR title
        using HttpRequestMessage prReq = new(HttpMethod.Get,
            $"repos/{repoFullName}/pulls/{prNumber}");
        SetAuthHeader(prReq, token);

        using var prResp = await client.SendAsync(prReq, ct)
            .ConfigureAwait(false);

        if (!prResp.IsSuccessStatusCode)
            return null;

        var prDetail = await prResp.Content
            .ReadFromJsonAsync(CodeReviewJsonContext.Default.GitHubPrDetail, ct)
            .ConfigureAwait(false);

        var title = prDetail?.Title ?? $"PR #{prNumber}";

        // Get diff
        using HttpRequestMessage diffReq = new(HttpMethod.Get,
            $"repos/{repoFullName}/pulls/{prNumber}");
        SetAuthHeader(diffReq, token);
        diffReq.Headers.Accept.Clear();
        diffReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.diff"));

        using var diffResp = await client.SendAsync(diffReq, ct)
            .ConfigureAwait(false);

        if (!diffResp.IsSuccessStatusCode)
            return null;

        var diff = await diffResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return (title, diff);
    }

    private async Task<string?> GetKnownErrorPatternsAsync(CancellationToken ct)
    {
        try
        {
            var recentIssues = await store
                .GetIssuesAsync(limit: 10, ct: ct)
                .ConfigureAwait(false);

            if (recentIssues.Count == 0)
                return null;

            return string.Join("\n",
                recentIssues.Select(static i =>
                    $"- {i.ErrorType}: {i.ErrorMessage ?? "no message"} (seen {i.EventCount}x)"));
        }
        catch (DuckDBException)
        {
            // DuckDB query failure is non-critical here — proceed without known patterns
            return null;
        }
    }

    private static CodeReviewComment[] ParseReviewComments(string text)
    {
        // Extract JSON array from potential markdown code blocks
        var jsonStart = text.IndexOf('[');
        var jsonEnd = text.LastIndexOf(']');
        if (jsonStart < 0 || jsonEnd <= jsonStart)
            return [];

        var json = text.AsSpan(jsonStart, jsonEnd - jsonStart + 1);
        try
        {
            return JsonSerializer.Deserialize(json, CodeReviewJsonContext.Default.CodeReviewCommentArray) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private HttpClient CreateGitHubClient(string _) =>
        // Return a clean client — auth is set per-request via SetAuthHeader to avoid
        // mutating DefaultRequestHeaders on a potentially shared HttpClient instance.
        httpClientFactory.CreateClient("GitHub");

    private static void SetAuthHeader(HttpRequestMessage request, string token) =>
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

    // ── LoggerMessage ───────────────────────────────────────────────────────

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Starting code review for {Repo} PR #{PrNumber}")]
    private partial void LogReviewStarted(string repo, int prNumber);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "No LLM configured — skipping code review")]
    private partial void LogNoLlmConfigured();

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Code review complete: {CommentCount} comments")]
    private partial void LogReviewComplete(int commentCount);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Code review failed")]
    private partial void LogReviewError(Exception ex);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Posting review comment on {File}:{Line}")]
    private partial void LogPostingComment(string file, int line);
}

// =============================================================================
// Records
// =============================================================================

// CodeReviewResult, CodeReviewComment, GitHubPrDetail, CodeReviewCommentPayload,
// CodeReviewJsonContext live in qyl.collector/Autofix/CodeReviewService.cs
