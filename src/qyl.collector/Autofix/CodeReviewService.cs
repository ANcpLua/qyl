using System.Net.Http.Headers;
using Microsoft.Extensions.AI;
using qyl.collector.Identity;

namespace qyl.collector.Autofix;

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
        string cacheKey = $"{repoFullName}#{prNumber}";
        LogReviewStarted(repoFullName, prNumber);

        if (llm is null)
        {
            LogNoLlmConfigured();
            return new CodeReviewResult
            {
                RepoFullName = repoFullName,
                PrNumber = prNumber,
                Comments = [],
                Reviewed = false
            };
        }

        (string Title, string Diff)? prData = await FetchPrDiffAsync(repoFullName, prNumber, ct)
            .ConfigureAwait(false);

        if (prData is null)
        {
            return new CodeReviewResult
            {
                RepoFullName = repoFullName,
                PrNumber = prNumber,
                Comments = [],
                Reviewed = false
            };
        }

        // Gather known error patterns from recent issues to feed context to the LLM
        string? knownPatterns = await GetKnownErrorPatternsAsync(ct).ConfigureAwait(false);

        string prompt = CodeReviewPrompt.Build(prData.Value.Title, prData.Value.Diff, knownPatterns);

        try
        {
            ChatResponse response = await llm.GetResponseAsync(prompt, cancellationToken: ct)
                .ConfigureAwait(false);

            string responseText = response.Text ?? "[]";
            CodeReviewComment[] comments = ParseReviewComments(responseText);

            LogReviewComplete(comments.Length);

            CodeReviewResult result = new()
            {
                RepoFullName = repoFullName,
                PrNumber = prNumber,
                Comments = comments,
                Reviewed = true
            };

            _cache[cacheKey] = result;
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogReviewError(ex);
            return new CodeReviewResult
            {
                RepoFullName = repoFullName,
                PrNumber = prNumber,
                Comments = [],
                Reviewed = false
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
        string? token = github.GetToken();
        if (token is null || comments.Count == 0)
            return false;

        using HttpClient client = CreateGitHubClient(token);
        bool allPosted = true;

        foreach (CodeReviewComment comment in comments)
        {
            LogPostingComment(comment.File, comment.Line);

            string body = comment.Suggestion is not null
                ? $"**[{comment.Severity}]** {comment.Comment}\n\n```suggestion\n{comment.Suggestion}\n```"
                : $"**[{comment.Severity}]** {comment.Comment}";

            var payload = new CodeReviewCommentPayload(body, comment.File, comment.Line);
            string json = JsonSerializer.Serialize(payload, CodeReviewJsonContext.Default.CodeReviewCommentPayload);
            using StringContent content = new(json, System.Text.Encoding.UTF8, "application/json");

            using HttpResponseMessage response = await client
                .PostAsync($"repos/{repoFullName}/pulls/{prNumber}/comments", content, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                allPosted = false;
        }

        return allPosted;
    }

    /// <summary>Returns a cached review result if one exists.</summary>
    public CodeReviewResult? GetCachedResult(string repoFullName, int prNumber)
    {
        string cacheKey = $"{repoFullName}#{prNumber}";
        return _cache.GetValueOrDefault(cacheKey);
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private async Task<(string Title, string Diff)?> FetchPrDiffAsync(
        string repoFullName, int prNumber, CancellationToken ct)
    {
        string? token = github.GetToken();
        if (token is null) return null;

        using HttpClient client = CreateGitHubClient(token);

        // Get PR title
        using HttpResponseMessage prResp = await client
            .GetAsync($"repos/{repoFullName}/pulls/{prNumber}", ct)
            .ConfigureAwait(false);

        if (!prResp.IsSuccessStatusCode)
            return null;

        GitHubPrDetail? prDetail = await prResp.Content
            .ReadFromJsonAsync(CodeReviewJsonContext.Default.GitHubPrDetail, ct)
            .ConfigureAwait(false);

        string title = prDetail?.Title ?? $"PR #{prNumber}";

        // Get diff
        using HttpRequestMessage diffReq = new(HttpMethod.Get,
            $"repos/{repoFullName}/pulls/{prNumber}");
        diffReq.Headers.Accept.Clear();
        diffReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.diff"));

        using HttpResponseMessage diffResp = await client.SendAsync(diffReq, ct)
            .ConfigureAwait(false);

        if (!diffResp.IsSuccessStatusCode)
            return null;

        string diff = await diffResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return (title, diff);
    }

    private async Task<string?> GetKnownErrorPatternsAsync(CancellationToken ct)
    {
        try
        {
            IReadOnlyList<IssueSummary> recentIssues = await store
                .GetIssuesAsync(limit: 10, ct: ct)
                .ConfigureAwait(false);

            if (recentIssues.Count == 0)
                return null;

            return string.Join("\n", recentIssues.Select(
                static i => $"- {i.ErrorType}: {i.ErrorMessage ?? "no message"} (seen {i.EventCount}x)"));
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
        int jsonStart = text.IndexOf('[');
        int jsonEnd = text.LastIndexOf(']');
        if (jsonStart < 0 || jsonEnd <= jsonStart)
            return [];

        ReadOnlySpan<char> json = text.AsSpan(jsonStart, jsonEnd - jsonStart + 1);
        try
        {
            return JsonSerializer.Deserialize(json, CodeReviewJsonContext.Default.CodeReviewCommentArray) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private HttpClient CreateGitHubClient(string token)
    {
        HttpClient client = httpClientFactory.CreateClient("GitHub");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

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

public sealed record CodeReviewResult
{
    public required string RepoFullName { get; init; }
    public required int PrNumber { get; init; }
    public required IReadOnlyList<CodeReviewComment> Comments { get; init; }
    public required bool Reviewed { get; init; }
}

public sealed record CodeReviewComment
{
    [JsonPropertyName("file")]
    public required string File { get; init; }

    [JsonPropertyName("line")]
    public required int Line { get; init; }

    [JsonPropertyName("severity")]
    public required string Severity { get; init; }

    [JsonPropertyName("comment")]
    public required string Comment { get; init; }

    [JsonPropertyName("suggestion")]
    public string? Suggestion { get; init; }
}

/// <summary>GitHub PR detail DTO — only the fields we need.</summary>
internal sealed record GitHubPrDetail(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("number")] int Number);

/// <summary>Payload for posting a PR review comment via GitHub API.</summary>
internal sealed record CodeReviewCommentPayload(
    [property: JsonPropertyName("body")] string Body,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("line")] int Line);

// =============================================================================
// Source-generated JSON context for code review types (AOT-compatible)
// =============================================================================

[JsonSerializable(typeof(CodeReviewComment[]))]
[JsonSerializable(typeof(CodeReviewResult))]
[JsonSerializable(typeof(GitHubPrDetail))]
[JsonSerializable(typeof(CodeReviewCommentPayload))]
internal sealed partial class CodeReviewJsonContext : JsonSerializerContext;
