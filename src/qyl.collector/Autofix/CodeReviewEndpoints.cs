namespace qyl.collector.Autofix;

/// <summary>
///     REST endpoints for triggering and querying AI-powered PR code reviews.
/// </summary>
public static class CodeReviewEndpoints
{
    public static void MapCodeReviewEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/code-review/{repoFullName}/pulls/{prNumber:int}", static async (
            string repoFullName, int prNumber,
            CodeReviewService reviewService, CancellationToken ct) =>
        {
            CodeReviewResult result = await reviewService
                .ReviewPullRequestAsync(repoFullName, prNumber, ct)
                .ConfigureAwait(false);

            return Results.Ok(result);
        });

        app.MapGet("/api/v1/code-review/{repoFullName}/pulls/{prNumber:int}", static (
            string repoFullName, int prNumber,
            CodeReviewService reviewService) =>
        {
            CodeReviewResult? cached = reviewService.GetCachedResult(repoFullName, prNumber);
            return cached is not null
                ? Results.Ok(cached)
                : Results.NotFound();
        });

        app.MapPost("/api/v1/code-review/{repoFullName}/pulls/{prNumber:int}/post", static async (
            string repoFullName, int prNumber,
            CodeReviewService reviewService, CancellationToken ct) =>
        {
            CodeReviewResult? cached = reviewService.GetCachedResult(repoFullName, prNumber);
            if (cached is null || cached.Comments.Count == 0)
                return Results.BadRequest(new { error = "No review comments available. Run a review first." });

            bool posted = await reviewService
                .PostReviewCommentsAsync(repoFullName, prNumber, cached.Comments, ct)
                .ConfigureAwait(false);

            return posted
                ? Results.Ok(new { posted = cached.Comments.Count })
                : Results.Problem("Failed to post some or all review comments to GitHub.");
        });
    }
}
