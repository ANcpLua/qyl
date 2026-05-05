using Microsoft.AspNetCore.Mvc;

namespace Qyl.Collector.Errors;

public static class IssueEndpoints
{
    [QylMapEndpoints]
    public static IEndpointRouteBuilder MapIssueEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/issues")
            .WithTags("Issues");


        group.MapGet("/", static async (
                [FromServices] IssueService service,
                string? projectId, string? status, string? priority, string? level,
                string? assignedTo, int? limit, int? offset,
                CancellationToken ct) =>
            {
                var issues = await service.ListIssuesAsync(
                    projectId, status, priority, level, assignedTo,
                    Math.Clamp(limit ?? 50, 1, 1000),
                    Math.Max(offset ?? 0, 0),
                    ct).ConfigureAwait(false);
                return TypedResults.Ok(new { items = issues, total = issues.Count });
            })
            .WithName("ListIssues")
            .WithSummary("List error issues with filtering");

        group.MapGet("/{issueId}", static async Task<IResult> (
                string issueId, [FromServices] IssueService service, CancellationToken ct) =>
            {
                var issue = await service.GetIssueByIdAsync(issueId, ct).ConfigureAwait(false);
                return issue is null ? TypedResults.NotFound() : TypedResults.Ok(issue);
            })
            .WithName("GetIssue")
            .WithSummary("Get a single error issue by ID");


        group.MapPatch("/{issueId}/status", static async Task<IResult> (
                string issueId, IssueStatusTransition body,
                [FromServices] IssueService service, CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(body.Status))
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["status"] = ["Status is required."]
                    });
                }

                try
                {
                    var updated = await service.TransitionStatusAsync(
                        issueId, body.Status, body.Reason, ct).ConfigureAwait(false);
                    return updated ? TypedResults.Ok() : TypedResults.NotFound();
                }
                catch (InvalidOperationException ex)
                {
                    return TypedResults.ValidationProblem(
                        new Dictionary<string, string[]> { ["status"] = [ex.Message] });
                }
            })
            .WithName("TransitionIssueStatus")
            .WithSummary("Transition issue status with lifecycle validation");

        group.MapPut("/{issueId}/assign", static async Task<IResult> (
                string issueId, IssueAssignment body,
                [FromServices] IssueService service, CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(body.Owner))
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["owner"] = ["Owner is required."]
                    });
                }

                var assigned = await service.AssignOwnerAsync(issueId, body.Owner, ct).ConfigureAwait(false);
                return assigned ? TypedResults.Ok() : TypedResults.NotFound();
            })
            .WithName("AssignIssue")
            .WithSummary("Assign an owner to an issue");

        group.MapPut("/{issueId}/priority", static async Task<IResult> (
                string issueId, IssuePriorityUpdate body,
                [FromServices] IssueService service, CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(body.Priority))
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["priority"] = ["Priority is required."]
                    });
                }

                var updated = await service.SetPriorityAsync(issueId, body.Priority, ct).ConfigureAwait(false);
                return updated ? TypedResults.Ok() : TypedResults.NotFound();
            })
            .WithName("SetIssuePriority")
            .WithSummary("Set issue priority");


        group.MapGet("/{issueId}/events", static async (
                string issueId, [FromServices] IssueService service, int? limit, CancellationToken ct) =>
            {
                var events = await service.GetEventsAsync(
                    issueId, Math.Clamp(limit ?? 100, 1, 1000), ct).ConfigureAwait(false);
                return TypedResults.Ok(new { items = events, total = events.Count });
            })
            .WithName("GetIssueEvents")
            .WithSummary("Get error events linked to an issue");


        group.MapGet("/{issueId}/events/{eventId}/breadcrumbs", static async (
                string _, string eventId,
                [FromServices] IssueService service, int? limit, CancellationToken ct) =>
            {
                var breadcrumbs = await service.GetBreadcrumbsAsync(
                    eventId, Math.Clamp(limit ?? 200, 1, 1000), ct).ConfigureAwait(false);
                return TypedResults.Ok(new { items = breadcrumbs, total = breadcrumbs.Count });
            })
            .WithName("GetEventBreadcrumbs")
            .WithSummary("Get breadcrumbs for an error event");

        return endpoints;
    }
}


public sealed record IssueStatusTransition(string? Status, string? Reason = null);

public sealed record IssueAssignment(string? Owner);

public sealed record IssuePriorityUpdate(string? Priority);
