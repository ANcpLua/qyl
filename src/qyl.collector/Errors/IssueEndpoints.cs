namespace qyl.collector.Errors;

/// <summary>
///     Minimal API endpoints for the v2 error issue engine.
///     Routes: <c>/api/v1/issues/*</c>
/// </summary>
public static class IssueEndpoints
{
    /// <summary>
    ///     Maps issue CRUD, lifecycle, event, and breadcrumb endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapIssueEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/issues")
            .WithTags("Issues");

        // --- Issue CRUD ---

        group.MapGet("/", static async (
            [Microsoft.AspNetCore.Mvc.FromServices] IssueService service,
            string? projectId, string? status, string? priority, string? level,
            string? assignedTo, int? limit, int? offset,
            CancellationToken ct) =>
        {
            var issues = await service.ListIssuesAsync(
                projectId, status, priority, level, assignedTo,
                Math.Clamp(limit ?? 50, 1, 1000),
                Math.Max(offset ?? 0, 0),
                ct).ConfigureAwait(false);
            return Results.Ok(new { items = issues, total = issues.Count });
        })
        .WithName("ListIssues")
        .WithSummary("List error issues with filtering");

        group.MapGet("/{issueId}", static async (
            string issueId, [Microsoft.AspNetCore.Mvc.FromServices] IssueService service, CancellationToken ct) =>
        {
            var issue = await service.GetIssueByIdAsync(issueId, ct).ConfigureAwait(false);
            return issue is null ? Results.NotFound() : Results.Ok(issue);
        })
        .WithName("GetIssue")
        .WithSummary("Get a single error issue by ID");

        // --- Issue Lifecycle ---

        group.MapPatch("/{issueId}/status", static async (
            string issueId, IssueStatusTransition body,
            [Microsoft.AspNetCore.Mvc.FromServices] IssueService service, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.Status))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["status"] = ["Status is required."]
                });

            try
            {
                var updated = await service.TransitionStatusAsync(
                    issueId, body.Status, body.Reason, ct).ConfigureAwait(false);
                return updated ? Results.Ok() : Results.NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["status"] = [ex.Message]
                });
            }
        })
        .WithName("TransitionIssueStatus")
        .WithSummary("Transition issue status with lifecycle validation");

        group.MapPut("/{issueId}/assign", static async (
            string issueId, IssueAssignment body,
            [Microsoft.AspNetCore.Mvc.FromServices] IssueService service, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.Owner))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["owner"] = ["Owner is required."]
                });

            var assigned = await service.AssignOwnerAsync(issueId, body.Owner, ct).ConfigureAwait(false);
            return assigned ? Results.Ok() : Results.NotFound();
        })
        .WithName("AssignIssue")
        .WithSummary("Assign an owner to an issue");

        group.MapPut("/{issueId}/priority", static async (
            string issueId, IssuePriorityUpdate body,
            [Microsoft.AspNetCore.Mvc.FromServices] IssueService service, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.Priority))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["priority"] = ["Priority is required."]
                });

            var updated = await service.SetPriorityAsync(issueId, body.Priority, ct).ConfigureAwait(false);
            return updated ? Results.Ok() : Results.NotFound();
        })
        .WithName("SetIssuePriority")
        .WithSummary("Set issue priority");

        // --- Issue Events ---

        group.MapGet("/{issueId}/events", static async (
            string issueId, [Microsoft.AspNetCore.Mvc.FromServices] IssueService service, int? limit, CancellationToken ct) =>
        {
            var events = await service.GetEventsAsync(
                issueId, Math.Clamp(limit ?? 100, 1, 1000), ct).ConfigureAwait(false);
            return Results.Ok(new { items = events, total = events.Count });
        })
        .WithName("GetIssueEvents")
        .WithSummary("Get error events linked to an issue");

        // --- Breadcrumbs ---

        group.MapGet("/{issueId}/events/{eventId}/breadcrumbs", static async (
            string issueId, string eventId,
            [Microsoft.AspNetCore.Mvc.FromServices] IssueService service, int? limit, CancellationToken ct) =>
        {
            var breadcrumbs = await service.GetBreadcrumbsAsync(
                eventId, Math.Clamp(limit ?? 200, 1, 1000), ct).ConfigureAwait(false);
            return Results.Ok(new { items = breadcrumbs, total = breadcrumbs.Count });
        })
        .WithName("GetEventBreadcrumbs")
        .WithSummary("Get breadcrumbs for an error event");

        return endpoints;
    }
}

// =============================================================================
// Request DTOs
// =============================================================================

/// <summary>Request body for issue status transition.</summary>
public sealed record IssueStatusTransition(string? Status, string? Reason = null);

/// <summary>Request body for issue owner assignment.</summary>
public sealed record IssueAssignment(string? Owner);

/// <summary>Request body for issue priority update.</summary>
public sealed record IssuePriorityUpdate(string? Priority);
