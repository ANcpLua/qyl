using System.Net.Http.Headers;
using System.Net.Http.Json;
using Qyl.Loom.Agents;

namespace Qyl.Loom.CodeReview;

public sealed partial class CodeReviewService(
    CollectorClient collector,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<CodeReviewService> logger,
    IQylLoomAgentsBuilder agents)
{
    private readonly ConcurrentDictionary<string, CodeReviewResult> _cache = new(StringComparer.Ordinal);

    public async Task<CodeReviewResult> ReviewPullRequestAsync(
        string repoFullName, int prNumber, CancellationToken ct = default)
    {
        LogReviewStarted(repoFullName, prNumber);

        if (!agents.IsConfigured)
        {
            LogNoLlmConfigured();
            return new CodeReviewResult
            {
                RepoFullName = repoFullName, PrNumber = prNumber, Comments = [], Reviewed = false
            };
        }

        var token = configuration["GITHUB_TOKEN"];
        if (string.IsNullOrEmpty(token))
        {
            LogNoGitHubToken();
            return new CodeReviewResult
            {
                RepoFullName = repoFullName, PrNumber = prNumber, Comments = [], Reviewed = false
            };
        }

        var prData = await FetchPrDiffAsync(repoFullName, prNumber, token, ct).ConfigureAwait(false);
        if (prData is null)
        {
            return new CodeReviewResult
            {
                RepoFullName = repoFullName, PrNumber = prNumber, Comments = [], Reviewed = false
            };
        }

        var knownPatterns = await GetKnownErrorPatternsAsync(ct).ConfigureAwait(false);
        var prompt = CodeReviewPrompt.Build(prData.Value.Title, prData.Value.Diff, knownPatterns);

        try
        {
            var agent = agents.BuildCodeReviewAgent();

            var response = await agent.RunAsync<CodeReviewComment[]>(
                prompt,
                serializerOptions: CodeReviewJsonContext.Default.Options,
                cancellationToken: ct).ConfigureAwait(false);
            var comments = response.Result ?? [];
            LogReviewComplete(comments.Length);

            CodeReviewResult result = new()
            {
                RepoFullName = repoFullName, PrNumber = prNumber, Comments = comments, Reviewed = true
            };

            _cache[$"{repoFullName}#{prNumber}"] = result;
            return result;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            LogReviewError(ex);
            return new CodeReviewResult
            {
                RepoFullName = repoFullName, PrNumber = prNumber, Comments = [], Reviewed = false
            };
        }
    }

    public async Task<bool> PostReviewCommentsAsync(
        string repoFullName, int prNumber,
        IReadOnlyList<CodeReviewComment> comments, CancellationToken ct = default)
    {
        var token = configuration["GITHUB_TOKEN"];
        if (string.IsNullOrEmpty(token) || comments.Count is 0)
            return false;

        var client = httpClientFactory.CreateClient("GitHub");
        var failed = 0;

        var options = new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct };
        await Parallel.ForEachAsync(comments, options, async (comment, perItemCt) =>
        {
            LogPostingComment(comment.File, comment.Line);

            var body = comment.Suggestion is not null
                ? $"**[{comment.Severity}]** {comment.Comment}\n\n```suggestion\n{comment.Suggestion}\n```"
                : $"**[{comment.Severity}]** {comment.Comment}";

            var payload = new CodeReviewCommentPayload(body, comment.File, comment.Line);
            var json = JsonSerializer.Serialize(payload, CodeReviewJsonContext.Default.CodeReviewCommentPayload);
            using StringContent content = new(json, Encoding.UTF8, "application/json");

            using HttpRequestMessage req = new(HttpMethod.Post,
                $"repos/{repoFullName}/pulls/{prNumber}/comments");
            req.Content = content;
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await client.SendAsync(req, perItemCt).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                Interlocked.Increment(ref failed);
        }).ConfigureAwait(false);

        return failed is 0;
    }

    public CodeReviewResult? GetCachedResult(string repoFullName, int prNumber) =>
        _cache.GetValueOrDefault($"{repoFullName}#{prNumber}");

    private async Task<(string Title, string Diff)?> FetchPrDiffAsync(
        string repoFullName, int prNumber, string token, CancellationToken ct)
    {
        using var client = httpClientFactory.CreateClient("GitHub");

        using HttpRequestMessage prReq = new(HttpMethod.Get, $"repos/{repoFullName}/pulls/{prNumber}");
        prReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var prResp = await client.SendAsync(prReq, ct).ConfigureAwait(false);
        if (!prResp.IsSuccessStatusCode)
            return null;

        var prDetail = await prResp.Content
            .ReadFromJsonAsync(CodeReviewJsonContext.Default.GitHubPrDetail, ct)
            .ConfigureAwait(false);
        var title = prDetail?.Title ?? $"PR #{prNumber}";

        using HttpRequestMessage diffReq = new(HttpMethod.Get, $"repos/{repoFullName}/pulls/{prNumber}");
        diffReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        diffReq.Headers.Accept.Clear();
        diffReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.diff"));

        using var diffResp = await client.SendAsync(diffReq, ct).ConfigureAwait(false);
        if (!diffResp.IsSuccessStatusCode)
            return null;

        var diff = await diffResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return (title, diff);
    }

    private async Task<string?> GetKnownErrorPatternsAsync(CancellationToken ct)
    {
        try
        {
            var issues = await collector.GetRecentIssuesAsync(10, ct).ConfigureAwait(false);
            if (issues.Count is 0) return null;

            return string.Join("\n",
                issues.Select(static i =>
                    $"- {i.ErrorType}: {i.ErrorMessage ?? "no message"} (seen {i.EventCount}x)"));
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting code review for {Repo} PR #{PrNumber}")]
    private partial void LogReviewStarted(string repo, int prNumber);

    [LoggerMessage(Level = LogLevel.Information, Message = "No LLM configured — skipping code review")]
    private partial void LogNoLlmConfigured();

    [LoggerMessage(Level = LogLevel.Warning, Message = "No GITHUB_TOKEN configured — skipping code review")]
    private partial void LogNoGitHubToken();

    [LoggerMessage(Level = LogLevel.Information, Message = "Code review complete: {CommentCount} comments")]
    private partial void LogReviewComplete(int commentCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Code review failed")]
    private partial void LogReviewError(Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Posting review comment on {File}:{Line}")]
    private partial void LogPostingComment(string file, int line);
}

public sealed record CodeReviewResult
{
    public required string RepoFullName { get; init; }
    public required int PrNumber { get; init; }
    public required IReadOnlyList<CodeReviewComment> Comments { get; init; }
    public required bool Reviewed { get; init; }
}

public sealed record CodeReviewComment
{
    [JsonPropertyName("file")] public required string File { get; init; }
    [JsonPropertyName("line")] public required int Line { get; init; }
    [JsonPropertyName("severity")] public required string Severity { get; init; }
    [JsonPropertyName("comment")] public required string Comment { get; init; }
    [JsonPropertyName("suggestion")] public string? Suggestion { get; init; }
}

public sealed record GitHubPrDetail(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("number")] int Number);

public sealed record CodeReviewCommentPayload(
    [property: JsonPropertyName("body")] string Body,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("line")] int Line);

[JsonSerializable(typeof(CodeReviewComment[]))]
[JsonSerializable(typeof(CodeReviewResult))]
[JsonSerializable(typeof(GitHubPrDetail))]
[JsonSerializable(typeof(CodeReviewCommentPayload))]
public sealed partial class CodeReviewJsonContext : JsonSerializerContext;
