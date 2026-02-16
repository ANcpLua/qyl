using Microsoft.AspNetCore.Mvc;

namespace qyl.collector.Provisioning;

/// <summary>
///     REST endpoints for instrumentation profile management and code generation.
/// </summary>
public static class ProfileEndpoints
{
    public static void MapProfileEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/config/profiles", static async (
            [FromServices] ProfileService service, CancellationToken ct) =>
        {
            var profiles = await service.GetProfilesAsync(ct);
            return Results.Ok(profiles);
        });

        app.MapPost("/api/v1/config/selections", static async (
            ConfigSelectionRequest request, [FromServices] ProfileService service, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.WorkspaceId))
                return Results.BadRequest(new { error = "WorkspaceId is required" });

            if (string.IsNullOrWhiteSpace(request.ProfileId))
                return Results.BadRequest(new { error = "ProfileId is required" });

            try
            {
                await service.SetSelectionAsync(request, ct);
                return Results.Ok(new { status = "ok" });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/v1/config/generation-jobs", static async (
            GenerationJobRequest request, [FromServices] GenerationJobService service, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.WorkspaceId))
                return Results.BadRequest(new { error = "WorkspaceId is required" });

            if (string.IsNullOrWhiteSpace(request.ProfileId))
                return Results.BadRequest(new { error = "ProfileId is required" });

            var job = await service.CreateJobAsync(request, ct);
            return Results.Created($"/api/v1/config/generation-jobs/{job.JobId}", job);
        });

        app.MapGet("/api/v1/config/generation-jobs/{jobId}", static async (
            string jobId, [FromServices] GenerationJobService service, CancellationToken ct) =>
        {
            var job = await service.GetJobAsync(jobId, ct);
            return job is null ? Results.NotFound() : Results.Ok(job);
        });
    }
}
