using qyl.collector.Storage;

namespace qyl.collector.CodingAgent;

public static class CodingAgentEndpoints
{
    public static void MapCodingAgentEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/fix-runs/{fixRunId}/coding-agents", static async (
            string fixRunId, LaunchCodingAgentRequest request,
            DuckDbStore store, CancellationToken ct) =>
        {
            if (await store.GetFixRunAsync(fixRunId, ct) is null)
                return Results.NotFound();

            if (!Enum.TryParse<CodingAgentProvider>(request.Provider, true, out var provider))
                provider = CodingAgentProvider.Loom;

            var record = new CodingAgentRunRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                FixRunId = fixRunId,
                Provider = provider.ToString().ToLowerInvariant(),
                Status = "pending",
                RepoFullName = request.RepoFullName
            };

            await store.InsertCodingAgentRunAsync(record, ct);
            return Results.Created($"/api/v1/fix-runs/{fixRunId}/coding-agents/{record.Id}", record);
        });

        app.MapGet("/api/v1/fix-runs/{fixRunId}/coding-agents", static async (
            string fixRunId, DuckDbStore store, int? limit, CancellationToken ct) =>
        {
            var runs = await store.GetCodingAgentRunsForFixRunAsync(
                fixRunId, Math.Clamp(limit ?? 50, 1, 1000), ct);
            return Results.Ok(new { items = runs, total = runs.Count });
        });

        app.MapGet("/api/v1/fix-runs/{fixRunId}/coding-agents/{id}", static async (
            string fixRunId, string id, DuckDbStore store, CancellationToken ct) =>
        {
            var run = await store.GetCodingAgentRunAsync(id, ct);
            if (run is null || run.FixRunId != fixRunId)
                return Results.NotFound();

            return Results.Ok(run);
        });

        app.MapPut("/api/v1/fix-runs/{fixRunId}/coding-agents/{id}", static async (
            string fixRunId, string id, UpdateCodingAgentRequest request,
            DuckDbStore store, CancellationToken ct) =>
        {
            var run = await store.GetCodingAgentRunAsync(id, ct);
            if (run is null || run.FixRunId != fixRunId)
                return Results.NotFound();

            await store.UpdateCodingAgentRunStatusAsync(
                id, request.Status, request.PrUrl, request.AgentUrl, ct);

            return Results.Ok(await store.GetCodingAgentRunAsync(id, ct));
        });
    }
}

public sealed record LaunchCodingAgentRequest(string? Provider = null, string? RepoFullName = null);

public sealed record UpdateCodingAgentRequest(
    string Status,
    string? PrUrl = null,
    string? AgentUrl = null);
