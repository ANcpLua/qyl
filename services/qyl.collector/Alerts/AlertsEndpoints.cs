// Copyright (c) 2025-2026 ancplua

using Microsoft.AspNetCore.Mvc;
using Qyl.Api;
using Qyl.Collector.Storage;
using Qyl.Common.Pagination;
using Qyl.Domains.Alerting;

namespace Qyl.Collector.Alerts;

/// <summary>
///     Minimal-API endpoints for alert-rule and alert-firing management.
///     Route: <c>/api/v1/alerts/*</c>. Mirrors the TypeSpec <c>AlertsApi</c> contract in
///     <c>core/specs/api/routes.tsp</c> — keep this file in sync with the generated
///     <c>IAlertsApi</c> surface when routes.tsp changes.
/// </summary>
public static class AlertsEndpoints
{
    /// <summary>Registers alert-rule and alert-firing HTTP endpoints under <c>/api/v1/alerts</c>.</summary>
    /// <param name="app">The ASP.NET Core web application to extend.</param>
    [QylMapEndpoints]
    public static void MapAlertsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/alerts");

        MapRuleRoutes(group);
        MapFiringRoutes(group);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Rule routes
    // ══════════════════════════════════════════════════════════════════════════

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
                Items = [.. rules],
                NextCursor = null,
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
                return TypedResults.BadRequest(new { error = "projectId, name, conditionJson, and targetType are required." });
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

    // ══════════════════════════════════════════════════════════════════════════
    // Firing routes (ack + resolve)
    // ══════════════════════════════════════════════════════════════════════════

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
