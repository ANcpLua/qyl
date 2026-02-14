namespace qyl.collector.Identity;

/// <summary>
///     Minimal API endpoints for workspace identity, project management, and onboarding.
///     Routes: /api/v1/workspaces/*, /api/v1/onboarding/*
/// </summary>
public static class IdentityEndpoints
{
    public static void MapIdentityEndpoints(this WebApplication app)
    {
        var workspaces = app.MapGroup("/api/v1/workspaces");
        MapWorkspaceRoutes(workspaces);

        var onboarding = app.MapGroup("/api/v1/onboarding");
        MapOnboardingRoutes(onboarding);
    }

    // ==========================================================================
    // Workspace Routes
    // ==========================================================================

    private static void MapWorkspaceRoutes(RouteGroupBuilder group)
    {
        group.MapGet("/", static async (
            WorkspaceService service, int? limit, CancellationToken ct) =>
        {
            var workspaces = await service.ListWorkspacesAsync(limit ?? 50, ct);
            return Results.Ok(new { items = workspaces, total = workspaces.Count });
        });

        group.MapGet("/{workspaceId}", static async (
            string workspaceId, WorkspaceService service, CancellationToken ct) =>
        {
            var workspace = await service.GetWorkspaceAsync(workspaceId, ct);
            return workspace is null ? Results.NotFound() : Results.Ok(workspace);
        });

        group.MapPost("/{workspaceId}/heartbeat", static async (
            string workspaceId, WorkspaceService service, CancellationToken ct) =>
        {
            var found = await service.HeartbeatAsync(workspaceId, ct);
            return found ? Results.Ok(new { status = "ok" }) : Results.NotFound();
        });

        group.MapDelete("/{workspaceId}", static async (
            string workspaceId, WorkspaceService service, CancellationToken ct) =>
        {
            var deleted = await service.DeleteWorkspaceAsync(workspaceId, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        // --- Project sub-routes under workspace ---

        group.MapGet("/{workspaceId}/projects", static async (
            string workspaceId, ProjectService service,
            int? limit, string? cursor, CancellationToken ct) =>
        {
            var projects = await service.ListProjectsAsync(workspaceId, limit ?? 50, cursor, ct);
            var nextCursor = projects.Count > 0 ? projects[^1].ProjectId : null;
            return Results.Ok(new { items = projects, total = projects.Count, next_cursor = nextCursor });
        });

        group.MapPost("/{workspaceId}/projects", static async (
            string workspaceId, CreateProjectRequest request,
            ProjectService service, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { error = "Name is required" });

            // Bind workspace ID from route
            var bound = request with { WorkspaceId = workspaceId };
            var project = await service.CreateProjectAsync(bound, ct);
            return Results.Created($"/api/v1/workspaces/{workspaceId}/projects/{project.ProjectId}", project);
        });

        group.MapGet("/{workspaceId}/projects/{projectId}", static async (
            string workspaceId, string projectId,
            ProjectService service, CancellationToken ct) =>
        {
            var project = await service.GetProjectAsync(projectId, ct);
            return project is null || project.WorkspaceId != workspaceId
                ? Results.NotFound()
                : Results.Ok(project);
        });

        group.MapDelete("/{workspaceId}/projects/{projectId}", static async (
            string workspaceId, string projectId,
            ProjectService service, CancellationToken ct) =>
        {
            var project = await service.GetProjectAsync(projectId, ct);
            if (project is null || project.WorkspaceId != workspaceId)
                return Results.NotFound();

            var deleted = await service.DeleteProjectAsync(projectId, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        // --- Environment sub-routes under project ---

        group.MapGet("/{workspaceId}/projects/{projectId}/environments", static async (
            string workspaceId, string projectId,
            ProjectService service, CancellationToken ct) =>
        {
            var envs = await service.ListEnvironmentsAsync(projectId, ct);
            return Results.Ok(new { items = envs, total = envs.Count });
        });

        group.MapPost("/{workspaceId}/projects/{projectId}/environments", static async (
            string workspaceId, string projectId,
            AddEnvironmentRequest request,
            ProjectService service, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { error = "Name is required" });

            var env = await service.AddEnvironmentAsync(projectId, request.Name, request.Description, ct);
            return Results.Created(
                $"/api/v1/workspaces/{workspaceId}/projects/{projectId}/environments/{env.EnvironmentId}",
                env);
        });

        group.MapDelete("/{workspaceId}/projects/{projectId}/environments/{environmentId}", static async (
            string workspaceId, string projectId, string environmentId,
            ProjectService service, CancellationToken ct) =>
        {
            var deleted = await service.DeleteEnvironmentAsync(environmentId, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        });
    }

    // ==========================================================================
    // Onboarding Routes (Handshake)
    // ==========================================================================

    private static void MapOnboardingRoutes(RouteGroupBuilder group)
    {
        group.MapPost("/handshake/start", static async (
            HandshakeRequest request, HandshakeService service, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.ServiceName))
                return Results.BadRequest(new { error = "ServiceName is required" });

            var response = await service.StartHandshakeAsync(request, ct);
            return Results.Ok(response);
        });

        group.MapPost("/handshake/verify", static async (
            HandshakeVerifyRequest request, HandshakeService service, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.WorkspaceId))
                return Results.BadRequest(new { error = "WorkspaceId is required" });

            if (string.IsNullOrWhiteSpace(request.CodeVerifier))
                return Results.BadRequest(new { error = "CodeVerifier is required" });

            var result = await service.VerifyHandshakeAsync(request.WorkspaceId, request.CodeVerifier, ct);
            return result is null
                ? Results.Unauthorized()
                : Results.Ok(result);
        });

        group.MapPost("/handshake/complete", static async (
            HandshakeResponse request, HandshakeService service, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.WorkspaceId))
                return Results.BadRequest(new { error = "WorkspaceId is required" });

            var response = await service.CompleteHandshakeAsync(request.WorkspaceId, ct);
            return response is null ? Results.NotFound() : Results.Ok(response);
        });
    }
}

// =============================================================================
// Endpoint-specific DTOs
// =============================================================================

/// <summary>
///     Request to add an environment to a project.
/// </summary>
public sealed record AddEnvironmentRequest(
    string Name,
    string? Description = null);

/// <summary>
///     Request to verify a handshake with a PKCE code_verifier.
/// </summary>
public sealed record HandshakeVerifyRequest(
    string WorkspaceId,
    string CodeVerifier);
