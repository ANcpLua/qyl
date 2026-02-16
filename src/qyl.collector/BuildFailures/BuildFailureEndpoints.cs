namespace qyl.collector.BuildFailures;

public static class BuildFailureEndpoints
{
    public static IEndpointRouteBuilder MapBuildFailureEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/build-failures", async (
            HttpContext context,
            BuildFailureIngestRequest request,
            IBuildFailureStore store,
            IConfiguration config,
            CancellationToken ct) =>
        {
            if (!IsAuthorized(context, config))
                return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Target))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["target"] = ["target is required"]
                });
            }

            var row = new BuildFailureRecord
            {
                Id = request.Id ?? Guid.NewGuid().ToString("N"),
                Timestamp = request.Timestamp ?? TimeProvider.System.GetUtcNow(),
                Target = request.Target,
                ExitCode = request.ExitCode,
                BinlogPath = request.BinlogPath,
                ErrorSummary = request.ErrorSummary,
                PropertyIssuesJson = request.PropertyIssuesJson,
                EnvReadsJson = request.EnvReadsJson,
                CallStackJson = request.CallStackJson,
                DurationMs = request.DurationMs
            };

            var id = await store.InsertAsync(row, ct).ConfigureAwait(false);
            return Results.Created($"/api/v1/build-failures/{id}", new { id });
        });

        endpoints.MapGet("/api/v1/build-failures", async (
            IBuildFailureStore store,
            int? limit,
            CancellationToken ct) =>
        {
            var rows = await store.ListAsync(limit ?? 10, ct).ConfigureAwait(false);
            return Results.Ok(new { items = rows.Select(Map).ToArray(), total = rows.Count });
        });

        endpoints.MapGet("/api/v1/build-failures/{id}", async (
            string id,
            IBuildFailureStore store,
            CancellationToken ct) =>
        {
            var row = await store.GetAsync(id, ct).ConfigureAwait(false);
            return row is null ? Results.NotFound() : Results.Ok(Map(row));
        });

        endpoints.MapGet("/api/v1/build-failures/search", async (
            string pattern,
            int? limit,
            IBuildFailureStore store,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["pattern"] = ["pattern is required"]
                });
            }

            var rows = await store.SearchAsync(pattern, limit ?? 50, ct).ConfigureAwait(false);
            return Results.Ok(new { items = rows.Select(Map).ToArray(), total = rows.Count });
        });

        return endpoints;
    }

    private static bool IsAuthorized(HttpContext context, IConfiguration config)
    {
        var expected = config["QYL_TOKEN"];
        if (string.IsNullOrWhiteSpace(expected))
            return true;

        if (context.Request.Headers.TryGetValue("x-qyl-token", out var values) &&
            values.Count > 0 &&
            string.Equals(values[0], expected, StringComparison.Ordinal))
        {
            return true;
        }

        if (!context.Request.Headers.TryGetValue("Authorization", out var authValues) || authValues.Count == 0)
            return false;

        const string bearerPrefix = "Bearer ";
        var auth = authValues[0];
        if (string.IsNullOrWhiteSpace(auth) ||
            !auth.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var token = auth[bearerPrefix.Length..].Trim();
        return string.Equals(token, expected, StringComparison.Ordinal);
    }

    private static BuildFailureResponse Map(BuildFailureRecord row) =>
        new(
            row.Id,
            row.Timestamp,
            row.Target,
            row.ExitCode,
            row.BinlogPath,
            row.ErrorSummary,
            row.PropertyIssuesJson,
            row.EnvReadsJson,
            row.CallStackJson,
            row.DurationMs,
            row.CreatedAt);
}
