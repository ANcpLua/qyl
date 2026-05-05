using Microsoft.AspNetCore.Mvc;

namespace Qyl.Collector.Provisioning;

public static class ProvisioningEndpoints
{
    [QylMapEndpoints]
    public static void MapProvisioningEndpoints(this WebApplication app)
    {
        var configurator = app.MapGroup("/api/v1/configurator");
        MapProfileRoutes(configurator);
        MapJobRoutes(configurator);
    }


    private static void MapProfileRoutes(RouteGroupBuilder group)
    {
        group.MapGet("/profiles", static async (
            [FromServices] GenerationProfileService service, CancellationToken ct) =>
        {
            var profiles = await service.GetProfilesAsync(ct);
            return TypedResults.Ok(new { items = profiles, total = profiles.Count });
        });

        group.MapGet("/profiles/{profileId}", static async Task<IResult> (
            string profileId, [FromServices] GenerationProfileService service, CancellationToken ct) =>
        {
            var profile = await service.GetProfileAsync(profileId, ct);
            return profile is null ? TypedResults.NotFound() : TypedResults.Ok(profile);
        });

        group.MapPost("/selections", static async Task<IResult> (
            GenerationSelectionRequest request, [FromServices] GenerationProfileService service,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.WorkspaceId))
                return TypedResults.BadRequest(new { error = "WorkspaceId is required" });

            if (string.IsNullOrWhiteSpace(request.ProfileId))
                return TypedResults.BadRequest(new { error = "ProfileId is required" });

            try
            {
                await service.SetSelectionAsync(request, ct);
                return TypedResults.Ok(new { status = "ok" });
            }
            catch (ArgumentException)
            {
                return TypedResults.BadRequest(new { error = "Request failed" });
            }
        });

        group.MapGet("/selections/{workspaceId}", static async Task<IResult> (
            string workspaceId, [FromServices] GenerationProfileService service, CancellationToken ct) =>
        {
            var selection = await service.GetSelectionAsync(workspaceId, ct);
            return selection is null ? TypedResults.NotFound() : TypedResults.Ok(selection);
        });
    }


    private static void MapJobRoutes(RouteGroupBuilder group)
    {
        group.MapPost("/jobs", static async Task<IResult> (
            GenerationJobRequest request, [FromServices] GenerationProfileService service, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.WorkspaceId))
                return TypedResults.BadRequest(new { error = "WorkspaceId is required" });

            if (string.IsNullOrWhiteSpace(request.ProfileId))
                return TypedResults.BadRequest(new { error = "ProfileId is required" });

            try
            {
                var job = await service.EnqueueJobAsync(request, ct);
                return TypedResults.Created($"/api/v1/configurator/jobs/{job.JobId}", job);
            }
            catch (ArgumentException)
            {
                return TypedResults.BadRequest(new { error = "Request failed" });
            }
        });

        group.MapGet("/jobs/{jobId}", static async Task<IResult> (
            string jobId, [FromServices] GenerationProfileService service, CancellationToken ct) =>
        {
            var job = await service.GetJobAsync(jobId, ct);
            return job is null ? TypedResults.NotFound() : TypedResults.Ok(job);
        });

        group.MapGet("/jobs", static async Task<IResult> (
            string workspaceId, [FromServices] GenerationProfileService service,
            int? limit, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(workspaceId))
                return TypedResults.BadRequest(new { error = "workspaceId query parameter is required" });

            var jobs = await service.ListJobsAsync(workspaceId, limit ?? 50, ct);
            return TypedResults.Ok(new { items = jobs, total = jobs.Count });
        });

        group.MapPost("/jobs/{jobId}/cancel", static async Task<IResult> (
            string jobId, [FromServices] GenerationProfileService service, CancellationToken ct) =>
        {
            var cancelled = await service.CancelJobAsync(jobId, ct);
            return cancelled
                ? TypedResults.Ok(new { jobId, status = "cancelled" })
                : TypedResults.NotFound();
        });
    }
}
