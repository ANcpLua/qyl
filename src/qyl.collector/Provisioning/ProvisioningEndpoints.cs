namespace qyl.collector.Provisioning;

/// <summary>
///     Minimal API endpoints for generation profile management and code generation jobs.
///     Routes: /api/v1/configurator/*
/// </summary>
public static class ProvisioningEndpoints
{
    public static void MapProvisioningEndpoints(this WebApplication app)
    {
        var configurator = app.MapGroup("/api/v1/configurator");
        MapProfileRoutes(configurator);
        MapJobRoutes(configurator);
    }

    // ==========================================================================
    // Profile Routes
    // ==========================================================================

    private static void MapProfileRoutes(RouteGroupBuilder group)
    {
        group.MapGet("/profiles", static async (
            [Microsoft.AspNetCore.Mvc.FromServices] GenerationProfileService service, CancellationToken ct) =>
        {
            var profiles = await service.GetProfilesAsync(ct);
            return Results.Ok(new { items = profiles, total = profiles.Count });
        });

        group.MapGet("/profiles/{profileId}", static async (
            string profileId, [Microsoft.AspNetCore.Mvc.FromServices] GenerationProfileService service, CancellationToken ct) =>
        {
            var profile = await service.GetProfileAsync(profileId, ct);
            return profile is null ? Results.NotFound() : Results.Ok(profile);
        });

        group.MapPost("/selections", static async (
            GenerationSelectionRequest request, [Microsoft.AspNetCore.Mvc.FromServices] GenerationProfileService service, CancellationToken ct) =>
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

        group.MapGet("/selections/{workspaceId}", static async (
            string workspaceId, [Microsoft.AspNetCore.Mvc.FromServices] GenerationProfileService service, CancellationToken ct) =>
        {
            var selection = await service.GetSelectionAsync(workspaceId, ct);
            return selection is null ? Results.NotFound() : Results.Ok(selection);
        });
    }

    // ==========================================================================
    // Generation Job Routes
    // ==========================================================================

    private static void MapJobRoutes(RouteGroupBuilder group)
    {
        group.MapPost("/jobs", static async (
            GenerationJobRequest request, [Microsoft.AspNetCore.Mvc.FromServices] GenerationProfileService service, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.WorkspaceId))
                return Results.BadRequest(new { error = "WorkspaceId is required" });

            if (string.IsNullOrWhiteSpace(request.ProfileId))
                return Results.BadRequest(new { error = "ProfileId is required" });

            try
            {
                var job = await service.EnqueueJobAsync(request, ct);
                return Results.Created($"/api/v1/configurator/jobs/{job.JobId}", job);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapGet("/jobs/{jobId}", static async (
            string jobId, [Microsoft.AspNetCore.Mvc.FromServices] GenerationProfileService service, CancellationToken ct) =>
        {
            var job = await service.GetJobAsync(jobId, ct);
            return job is null ? Results.NotFound() : Results.Ok(job);
        });

        group.MapGet("/jobs", static async (
            string workspaceId, [Microsoft.AspNetCore.Mvc.FromServices] GenerationProfileService service,
            int? limit, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(workspaceId))
                return Results.BadRequest(new { error = "workspaceId query parameter is required" });

            var jobs = await service.ListJobsAsync(workspaceId, limit ?? 50, ct);
            return Results.Ok(new { items = jobs, total = jobs.Count });
        });

        group.MapPost("/jobs/{jobId}/cancel", static async (
            string jobId, [Microsoft.AspNetCore.Mvc.FromServices] GenerationProfileService service, CancellationToken ct) =>
        {
            var cancelled = await service.CancelJobAsync(jobId, ct);
            return cancelled
                ? Results.Ok(new { jobId, status = "cancelled" })
                : Results.NotFound();
        });
    }
}
