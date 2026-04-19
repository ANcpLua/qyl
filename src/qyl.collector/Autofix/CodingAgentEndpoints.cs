using Qyl.Contracts.Loom;

namespace Qyl.Collector.Autofix;

public static class CodingAgentEndpoints
{
    [QylMapEndpoints]
    public static void MapCodingAgentEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/fix-runs/{fixRunId}/coding-agents", static async Task<IResult> (
            string fixRunId,
            LaunchCodingAgentRequest request,
            DuckDbStore store,
            CancellationToken ct) =>
        {
            if (await store.GetFixRunAsync(fixRunId, ct).ConfigureAwait(false) is null)
                return TypedResults.NotFound();

            var record = new CodingAgentRunRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                FixRunId = fixRunId,
                Provider = CodingAgentProviderNames.NormalizeSlug(request.Provider),
                Status = "pending",
                RepoFullName = request.RepoFullName,
                CreatedAt = TimeProvider.System.GetUtcNow().UtcDateTime
            };

            await store.InsertCodingAgentRunAsync(record, ct).ConfigureAwait(false);
            var persisted = await store.GetCodingAgentRunAsync(record.Id, ct).ConfigureAwait(false) ?? record;
            return TypedResults.Created($"/api/v1/fix-runs/{fixRunId}/coding-agents/{persisted.Id}", persisted);
        });

        app.MapGet("/api/v1/fix-runs/{fixRunId}/coding-agents", static async Task<IResult> (
            string fixRunId,
            DuckDbStore store,
            int? limit,
            CancellationToken ct) =>
        {
            var runs = await store.GetCodingAgentRunsForFixRunAsync(
                    fixRunId,
                    Math.Clamp(limit ?? 50, 1, 1000),
                    ct)
                .ConfigureAwait(false);

            return TypedResults.Ok(new { items = runs, total = runs.Count });
        });

        app.MapGet("/api/v1/fix-runs/{fixRunId}/coding-agents/{id}", static async Task<IResult> (
            string fixRunId,
            string id,
            DuckDbStore store,
            CancellationToken ct) =>
        {
            var run = await store.GetCodingAgentRunAsync(id, ct).ConfigureAwait(false);
            if (run is null || !string.Equals(run.FixRunId, fixRunId, StringComparison.Ordinal))
                return TypedResults.NotFound();

            return TypedResults.Ok(run);
        });

        app.MapPut("/api/v1/fix-runs/{fixRunId}/coding-agents/{id}", static async Task<IResult> (
            string fixRunId,
            string id,
            UpdateCodingAgentRequest request,
            DuckDbStore store,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Status))
                return TypedResults.BadRequest(new { error = "Status is required." });

            var run = await store.GetCodingAgentRunAsync(id, ct).ConfigureAwait(false);
            if (run is null || !string.Equals(run.FixRunId, fixRunId, StringComparison.Ordinal))
                return TypedResults.NotFound();

            await store.UpdateCodingAgentRunStatusAsync(
                    id,
                    request.Status.Trim(),
                    request.PrUrl,
                    request.AgentUrl,
                    ct)
                .ConfigureAwait(false);

            return TypedResults.Ok(await store.GetCodingAgentRunAsync(id, ct).ConfigureAwait(false));
        });
    }
}

public sealed record LaunchCodingAgentRequest(string? Provider = null, string? RepoFullName = null);

public sealed record UpdateCodingAgentRequest(
    string Status,
    string? PrUrl = null,
    string? AgentUrl = null);
