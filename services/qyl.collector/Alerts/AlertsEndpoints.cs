
using Microsoft.AspNetCore.Mvc;
using Qyl.Api.Contracts.Common.Pagination;
using Qyl.Api.Contracts.Domains.Alerting;

namespace Qyl.Collector.Alerts;

public static class AlertsEndpoints
{
    [QylMapEndpoints]
    public static void MapAlertsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/alerts");

        MapRuleRoutes(group);
        MapFiringRoutes(group);
    }


    private static void MapRuleRoutes(RouteGroupBuilder group)
    {
        group.MapGet("/rules", static async Task<IResult> (
            [FromServices] DuckDbStore store,
            [FromQuery] string? projectId,
            [FromQuery] bool? enabled,
            [FromQuery] int? limit,
            CancellationToken ct) =>
        {
            var rules = await store.ListAlertRulesAsync(projectId, enabled, limit ?? 20, ct);
            return TypedResults.Ok(new CursorPageAlertRuleEntity
            {
                Items = [.. rules], NextCursor = null, HasMore = false
            });
        });

        group.MapGet("/rules/{ruleId}", static async Task<IResult> (
            string ruleId,
            [FromServices] DuckDbStore store,
            CancellationToken ct) =>
        {
            var rule = await store.GetAlertRuleAsync(ruleId, ct);
            return rule is null ? TypedResults.NotFound() : TypedResults.Ok(rule);
        });

        group.MapPost("/rules", static async Task<IResult> (
            AlertRuleEntity rule,
            [FromServices] DuckDbStore store,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(rule.ProjectId) || string.IsNullOrWhiteSpace(rule.Name) ||
                string.IsNullOrWhiteSpace(rule.ConditionJson) || string.IsNullOrWhiteSpace(rule.TargetType))
            {
                return TypedResults.BadRequest(new
                {
                    error = "projectId, name, conditionJson, and targetType are required."
                });
            }

            var persisted = await store.InsertAlertRuleAsync(rule, ct);
            return TypedResults.Created($"/api/v1/alerts/rules/{persisted.Id}", persisted);
        });

        group.MapPut("/rules/{ruleId}", static async Task<IResult> (
            string ruleId,
            AlertRuleEntity rule,
            [FromServices] DuckDbStore store,
            CancellationToken ct) =>
        {
            var updated = await store.UpdateAlertRuleAsync(ruleId, rule, ct);
            return updated is null ? TypedResults.NotFound() : TypedResults.Ok(updated);
        });

        group.MapDelete("/rules/{ruleId}", static async Task<IResult> (
            string ruleId,
            [FromServices] DuckDbStore store,
            CancellationToken ct) =>
        {
            var deleted = await store.DeleteAlertRuleAsync(ruleId, ct);
            return deleted ? TypedResults.NoContent() : TypedResults.NotFound();
        });
    }


    private static void MapFiringRoutes(RouteGroupBuilder group)
    {
        group.MapPost("/firings/{firingId}/acknowledge", static async Task<IResult> (
            string firingId,
            AlertFiringAcknowledgement body,
            [FromServices] DuckDbStore store,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.AcknowledgedBy))
                return TypedResults.BadRequest(new { error = "acknowledgedBy is required." });

            var firing = await store.AcknowledgeAlertFiringAsync(firingId, body.AcknowledgedBy, ct);
            return firing is null ? TypedResults.NotFound() : TypedResults.Ok(firing);
        });

        group.MapPost("/firings/{firingId}/resolve", static async Task<IResult> (
            string firingId,
            [FromServices] DuckDbStore store,
            CancellationToken ct) =>
        {
            var firing = await store.ResolveAlertFiringAsync(firingId, ct);
            return firing is null ? TypedResults.NotFound() : TypedResults.Ok(firing);
        });
    }
}

public sealed record AlertFiringAcknowledgement(string? AcknowledgedBy);
