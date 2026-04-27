using Microsoft.AspNetCore.Mvc;

namespace Qyl.Collector.Identity;

/// <summary>
///     Minimal API endpoints for workspace identity, project management, and onboarding.
///     Routes: /api/v1/workspaces/*, /api/v1/onboarding/*
/// </summary>
public static class IdentityEndpoints
{
    [QylMapEndpoints]
    public static void MapIdentityEndpoints(this WebApplication app)
    {
        var workspaces = app.MapGroup("/api/v1/workspaces");
        MapWorkspaceRoutes(workspaces);

        var onboarding = app.MapGroup("/api/v1/onboarding");
        MapOnboardingRoutes(onboarding);

        var mcpProjects = app.MapGroup("/api/v1/mcp/projects");
        MapMcpProjectRoutes(mcpProjects);
    }

    // ==========================================================================
    // Workspace Routes
    // ==========================================================================

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

        // --- Project sub-routes under workspace ---

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

            // Bind workspace ID from route
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

        // --- Environment sub-routes under project ---

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

    // ==========================================================================
    // MCP Project Routes
    // ==========================================================================

    private static void MapMcpProjectRoutes(RouteGroupBuilder group)
    {
        group.MapMethods("/{slug}", ["PATCH"], static async Task<IResult> (
            string slug, McpUpdateProjectRequest request,
            [FromServices] DuckDbStore store, CancellationToken ct) =>
        {
            var affected = await store.ExecuteWriteAsync(async (con, token) =>
            {
                await using var cmd = con.CreateCommand();
                cmd.CommandText = """
                                  UPDATE projects
                                  SET description = COALESCE($1, description),
                                      name = COALESCE($2, name),
                                      updated_at = $3
                                  WHERE slug = $4
                                  """;
                cmd.Parameters.Add(new DuckDBParameter { Value = request.Description ?? (object)DBNull.Value });
                cmd.Parameters.Add(new DuckDBParameter { Value = request.Name ?? (object)DBNull.Value });
                cmd.Parameters.Add(new DuckDBParameter { Value = TimeProvider.System.GetUtcNow().UtcDateTime });
                cmd.Parameters.Add(new DuckDBParameter { Value = slug });
                return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }, ct).ConfigureAwait(false);

            return affected > 0
                ? TypedResults.Ok(new { slug, updated = true })
                : TypedResults.NotFound();
        });

        group.MapMethods("/{slug}/retention", ["PATCH"], static async Task<IResult> (
            string slug, McpRetentionRequest request,
            [FromServices] DuckDbStore store, CancellationToken ct) =>
        {
            if (request.RetentionDays is null or < 1)
                return TypedResults.BadRequest(new { error = "retention_days must be >= 1" });

            await store.ExecuteWriteAsync(async (con, token) =>
            {
                // Ensure retention config table exists
                await using var ddlCmd = con.CreateCommand();
                ddlCmd.CommandText = """
                                     CREATE TABLE IF NOT EXISTS project_retention (
                                         slug VARCHAR NOT NULL PRIMARY KEY,
                                         retention_days INTEGER NOT NULL,
                                         updated_at TIMESTAMP NOT NULL
                                     )
                                     """;
                await ddlCmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);

                await using var cmd = con.CreateCommand();
                cmd.CommandText = """
                                  INSERT INTO project_retention (slug, retention_days, updated_at)
                                  VALUES ($1, $2, $3)
                                  ON CONFLICT (slug) DO UPDATE SET
                                      retention_days = EXCLUDED.retention_days,
                                      updated_at = EXCLUDED.updated_at
                                  """;
                cmd.Parameters.Add(new DuckDBParameter { Value = slug });
                cmd.Parameters.Add(new DuckDBParameter { Value = request.RetentionDays.Value });
                cmd.Parameters.Add(new DuckDBParameter { Value = TimeProvider.System.GetUtcNow().UtcDateTime });
                return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }, ct).ConfigureAwait(false);

            return TypedResults.Ok(new { slug, retention_days = request.RetentionDays.Value });
        });
    }

    // ==========================================================================
    // Onboarding Routes (Handshake)
    // ==========================================================================

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

public sealed record McpUpdateProjectRequest
{
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
}

public sealed record McpRetentionRequest
{
    [JsonPropertyName("retention_days")] public int? RetentionDays { get; init; }
}
