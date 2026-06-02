using Microsoft.AspNetCore.Mvc;

namespace Qyl.Collector.Identity;

public static class IdentityEndpoints
{
    public static void MapIdentityEndpoints(this WebApplication app)
    {
        var workspaces = app.MapGroup("/api/v1/workspaces");
        MapWorkspaceRoutes(workspaces);

        var onboarding = app.MapGroup("/api/v1/onboarding");
        MapOnboardingRoutes(onboarding);

    }


    private static void MapWorkspaceRoutes(RouteGroupBuilder group)
    {
        group.MapGet("/", static async (
            [FromServices] WorkspaceService service, int? limit, CancellationToken ct) =>
        {
            var workspaces = await service.ListWorkspacesAsync(limit ?? 50, ct);
            return TypedResults.Ok(new { items = workspaces, total = workspaces.Count });
        });

        group.MapGet("/{workspaceId}", static async Task<IResult> (
            string workspaceId, [FromServices] WorkspaceService service, CancellationToken ct) =>
        {
            var workspace = await service.GetWorkspaceAsync(workspaceId, ct);
            return workspace is null ? TypedResults.NotFound() : TypedResults.Ok(workspace);
        });

        group.MapPost("/{workspaceId}/heartbeat", static async Task<IResult> (
            string workspaceId, [FromServices] WorkspaceService service, CancellationToken ct) =>
        {
            var found = await service.HeartbeatAsync(workspaceId, ct);
            return found ? TypedResults.Ok(new { status = "ok" }) : TypedResults.NotFound();
        });

        group.MapDelete("/{workspaceId}", static async Task<IResult> (
            string workspaceId, [FromServices] WorkspaceService service, CancellationToken ct) =>
        {
            var deleted = await service.DeleteWorkspaceAsync(workspaceId, ct);
            return deleted ? TypedResults.NoContent() : TypedResults.NotFound();
        });


        group.MapGet("/{workspaceId}/projects", static async (
            string workspaceId, [FromServices] ProjectService service,
            int? limit, string? cursor, CancellationToken ct) =>
        {
            var projects = await service.ListProjectsAsync(workspaceId, limit ?? 50, cursor, ct);
            var nextCursor = projects.Count > 0 ? projects[^1].ProjectId : null;
            return TypedResults.Ok(new { items = projects, total = projects.Count, next_cursor = nextCursor });
        });

        group.MapPost("/{workspaceId}/projects", static async Task<IResult> (
            string workspaceId, CreateProjectRequest request,
            [FromServices] ProjectService service, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return TypedResults.BadRequest(new { error = "Name is required" });

            var bound = request with { WorkspaceId = workspaceId };
            var project = await service.CreateProjectAsync(bound, ct);
            return TypedResults.Created($"/api/v1/workspaces/{workspaceId}/projects/{project.ProjectId}", project);
        });

        group.MapGet("/{workspaceId}/projects/{projectId}", static async Task<IResult> (
            string workspaceId, string projectId,
            [FromServices] ProjectService service, CancellationToken ct) =>
        {
            var project = await service.GetProjectAsync(projectId, ct);
            return project is null || project.WorkspaceId != workspaceId
                ? TypedResults.NotFound()
                : TypedResults.Ok(project);
        });

        group.MapDelete("/{workspaceId}/projects/{projectId}", static async Task<IResult> (
            string workspaceId, string projectId,
            [FromServices] ProjectService service, CancellationToken ct) =>
        {
            var project = await service.GetProjectAsync(projectId, ct);
            if (project is null || project.WorkspaceId != workspaceId)
                return TypedResults.NotFound();

            var deleted = await service.DeleteProjectAsync(projectId, ct);
            return deleted ? TypedResults.NoContent() : TypedResults.NotFound();
        });


        group.MapGet("/{workspaceId}/projects/{projectId}/environments", static async (
            string workspaceId, string projectId,
            [FromServices] ProjectService service, CancellationToken ct) =>
        {
            Guard.NotNullOrWhiteSpace(workspaceId);
            var envs = await service.ListEnvironmentsAsync(projectId, ct);
            return TypedResults.Ok(new { items = envs, total = envs.Count });
        });

        group.MapPost("/{workspaceId}/projects/{projectId}/environments", static async Task<IResult> (
            string workspaceId, string projectId,
            AddEnvironmentRequest request,
            [FromServices] ProjectService service, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return TypedResults.BadRequest(new { error = "Name is required" });

            var env = await service.AddEnvironmentAsync(projectId, request.Name, request.Description, ct);
            return TypedResults.Created(
                $"/api/v1/workspaces/{workspaceId}/projects/{projectId}/environments/{env.EnvironmentId}",
                env);
        });

        group.MapDelete("/{workspaceId}/projects/{projectId}/environments/{environmentId}", static async Task<IResult> (
            string workspaceId, string projectId, string environmentId,
            [FromServices] ProjectService service, CancellationToken ct) =>
        {
            Guard.NotNullOrWhiteSpace(workspaceId);
            Guard.NotNullOrWhiteSpace(projectId);
            var deleted = await service.DeleteEnvironmentAsync(environmentId, ct);
            return deleted ? TypedResults.NoContent() : TypedResults.NotFound();
        });
    }

    private static void MapOnboardingRoutes(RouteGroupBuilder group)
    {
        group.MapPost("/handshake/start", static async Task<IResult> (
            HandshakeRequest request, [FromServices] HandshakeService service, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.ServiceName))
                return TypedResults.BadRequest(new { error = "ServiceName is required" });

            var response = await service.StartHandshakeAsync(request, ct);
            return TypedResults.Ok(response);
        });

        group.MapPost("/handshake/verify", static async Task<IResult> (
            HandshakeVerifyRequest request, [FromServices] HandshakeService service, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.WorkspaceId))
                return TypedResults.BadRequest(new { error = "WorkspaceId is required" });

            if (string.IsNullOrWhiteSpace(request.CodeVerifier))
                return TypedResults.BadRequest(new { error = "CodeVerifier is required" });

            var result = await service.VerifyHandshakeAsync(request.WorkspaceId, request.CodeVerifier, ct);
            return result is null
                ? TypedResults.Unauthorized()
                : TypedResults.Ok(result);
        });

        group.MapPost("/handshake/complete", static async Task<IResult> (
            HandshakeResponse request, [FromServices] HandshakeService service, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.WorkspaceId))
                return TypedResults.BadRequest(new { error = "WorkspaceId is required" });

            var response = await service.CompleteHandshakeAsync(request.WorkspaceId, ct);
            return response is null ? TypedResults.NotFound() : TypedResults.Ok(response);
        });
    }
}


public sealed record AddEnvironmentRequest(
    string Name,
    string? Description = null);

public sealed record HandshakeVerifyRequest(
    string WorkspaceId,
    string CodeVerifier);
