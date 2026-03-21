namespace Qyl.Collector.Autofix;

/// <summary>
///     REST endpoints for triggering and querying AI-powered PR code reviews.
/// </summary>
public static class CodeReviewEndpoints
{
    public static void MapCodeReviewEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/code-review/{owner}/{repo}/pulls/{prNumber:int}", static async (
            string owner, string repo, int prNumber,
            CodeReviewService reviewService, CancellationToken ct) =>
        {
            var repoFullName = $"{owner}/{repo}";
            var result = await reviewService
                .ReviewPullRequestAsync(repoFullName, prNumber, ct)
                .ConfigureAwait(false);

            return TypedResults.Ok(result);
        });

        app.MapGet("/api/v1/code-review/{owner}/{repo}/pulls/{prNumber:int}", static IResult (
            string owner, string repo, int prNumber,
            CodeReviewService reviewService) =>
        {
            var repoFullName = $"{owner}/{repo}";
            var cached = reviewService.GetCachedResult(repoFullName, prNumber);
            return cached is not null
                ? TypedResults.Ok(cached)
                : TypedResults.NotFound();
        });

        app.MapPost("/api/v1/code-review/{owner}/{repo}/pulls/{prNumber:int}/post", static async Task<IResult> (
            string owner, string repo, int prNumber,
            CodeReviewService reviewService, CancellationToken ct) =>
        {
            var repoFullName = $"{owner}/{repo}";
            var cached = reviewService.GetCachedResult(repoFullName, prNumber);
            if (cached is null || cached.Comments.Count == 0)
                return TypedResults.BadRequest(new { error = "No review comments available. Run a review first." });

            var posted = await reviewService
                .PostReviewCommentsAsync(repoFullName, prNumber, cached.Comments, ct)
                .ConfigureAwait(false);

            return posted
                ? TypedResults.Ok(new { posted = cached.Comments.Count })
                : TypedResults.Problem("Failed to post some or all review comments to GitHub.");
        });
    }
}
