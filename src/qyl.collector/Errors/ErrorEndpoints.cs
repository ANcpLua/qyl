using qyl.collector.Storage;

namespace qyl.collector.Errors;

public static class ErrorEndpoints
{
    private static readonly HashSet<string> AllowedStatuses = ["new", "acknowledged", "resolved", "ignored"];

    public static void MapErrorEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/errors", static async (
            DuckDbStore store, string? category, string? status, string? serviceName,
            int? limit, CancellationToken ct) =>
        {
            var errors = await store.GetErrorsAsync(category, status, serviceName, Math.Clamp(limit ?? 50, 1, 1000), ct);
            return Results.Ok(new { items = errors, total = errors.Count });
        });

        app.MapGet("/api/v1/errors/stats", static async (DuckDbStore store, CancellationToken ct) =>
        {
            var stats = await store.GetErrorStatsAsync(ct);
            return Results.Ok(stats);
        });

        app.MapGet("/api/v1/errors/{errorId}", static async (
            string errorId, DuckDbStore store, CancellationToken ct) =>
        {
            var error = await store.GetErrorByIdAsync(errorId, ct);
            return error is null ? Results.NotFound() : Results.Ok(error);
        });

        app.MapPatch("/api/v1/errors/{errorId}", static async (
            string errorId, ErrorStatusUpdate update, DuckDbStore store, CancellationToken ct) =>
        {
            if (!AllowedStatuses.Contains(update.Status))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["status"] = [$"Must be one of: {string.Join(", ", AllowedStatuses)}"]
                });

            var existing = await store.GetErrorByIdAsync(errorId, ct);
            if (existing is null)
                return Results.NotFound();

            await store.UpdateErrorStatusAsync(errorId, update.Status, update.AssignedTo, ct);
            return Results.Ok();
        });
    }
}

public sealed record ErrorStatusUpdate(string Status, string? AssignedTo = null);
