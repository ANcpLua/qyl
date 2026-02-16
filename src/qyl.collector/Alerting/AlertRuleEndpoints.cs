using Microsoft.AspNetCore.Mvc;

namespace qyl.collector.Alerting;

/// <summary>
///     Minimal API endpoints for alert rule and firing management.
///     Routes: <c>/api/v1/alerts/*</c>
/// </summary>
public static class AlertRuleEndpoints
{
    /// <summary>
    ///     Maps alert rule CRUD, firing management, and fix run endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapAlertRuleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/alerts")
            .WithTags("AlertRules");

        // --- Alert Rules ---

        group.MapGet("/rules", static async (
                [FromServices] AlertRuleService service,
                string? projectId, string? ruleType, bool? enabled,
                int? limit, CancellationToken ct) =>
            {
                var rules = await service.ListRulesAsync(
                    projectId, ruleType, enabled,
                    Math.Clamp(limit ?? 100, 1, 1000), ct).ConfigureAwait(false);
                return Results.Ok(new { items = rules, total = rules.Count });
            })
            .WithName("ListAlertRules")
            .WithSummary("List alert rules with filtering");

        group.MapGet("/rules/{ruleId}", static async (
                string ruleId, [FromServices] AlertRuleService service, CancellationToken ct) =>
            {
                var rule = await service.GetRuleByIdAsync(ruleId, ct).ConfigureAwait(false);
                return rule is null ? Results.NotFound() : Results.Ok(rule);
            })
            .WithName("GetAlertRule")
            .WithSummary("Get a single alert rule by ID");

        group.MapPost("/rules", static async (
                CreateAlertRuleRequest body, [FromServices] AlertRuleService service, CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(body.ProjectId) ||
                    string.IsNullOrWhiteSpace(body.Name) ||
                    string.IsNullOrWhiteSpace(body.RuleType) ||
                    string.IsNullOrWhiteSpace(body.ConditionJson) ||
                    string.IsNullOrWhiteSpace(body.TargetType))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["body"] = ["projectId, name, ruleType, conditionJson, and targetType are required."]
                    });
                }

                var ruleId = await service.CreateRuleAsync(
                    body.ProjectId, body.Name, body.RuleType, body.ConditionJson, body.TargetType,
                    body.Description, body.ThresholdJson, body.TargetFilterJson,
                    body.Severity ?? "warning", body.CooldownSeconds ?? 300,
                    body.NotificationChannelsJson, ct).ConfigureAwait(false);
                return Results.Created($"/api/v1/alerts/rules/{ruleId}", new { id = ruleId });
            })
            .WithName("CreateAlertRule")
            .WithSummary("Create a new alert rule");

        group.MapPatch("/rules/{ruleId}/enabled", static async (
                string ruleId, RuleEnabledUpdate body,
                [FromServices] AlertRuleService service, CancellationToken ct) =>
            {
                var updated = await service.SetRuleEnabledAsync(ruleId, body.Enabled, ct).ConfigureAwait(false);
                return updated ? Results.Ok() : Results.NotFound();
            })
            .WithName("SetAlertRuleEnabled")
            .WithSummary("Enable or disable an alert rule");

        group.MapDelete("/rules/{ruleId}", static async (
                string ruleId, [FromServices] AlertRuleService service, CancellationToken ct) =>
            {
                var deleted = await service.DeleteRuleAsync(ruleId, ct).ConfigureAwait(false);
                return deleted ? Results.NoContent() : Results.NotFound();
            })
            .WithName("DeleteAlertRule")
            .WithSummary("Delete an alert rule");

        // --- Alert Firings ---

        group.MapGet("/firings", static async (
                [FromServices] AlertRuleService service,
                string? ruleId, string? status, int? limit,
                CancellationToken ct) =>
            {
                var firings = await service.ListFiringsAsync(
                    ruleId, status, Math.Clamp(limit ?? 100, 1, 1000), ct).ConfigureAwait(false);
                return Results.Ok(new { items = firings, total = firings.Count });
            })
            .WithName("ListAlertFirings")
            .WithSummary("List alert firings with filtering");

        group.MapPost("/firings/{firingId}/acknowledge", static async (
                string firingId, AcknowledgeRequest body,
                [FromServices] AlertRuleService service, CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(body.AcknowledgedBy))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["acknowledgedBy"] = ["acknowledgedBy is required."]
                    });
                }

                var acked = await service.AcknowledgeFiringAsync(firingId, body.AcknowledgedBy, ct)
                    .ConfigureAwait(false);
                return acked ? Results.Ok() : Results.NotFound();
            })
            .WithName("AcknowledgeAlertFiring")
            .WithSummary("Acknowledge a firing alert");

        group.MapPost("/firings/{firingId}/resolve", static async (
                string firingId, [FromServices] AlertRuleService service, CancellationToken ct) =>
            {
                var resolved = await service.ResolveFiringAsync(firingId, ct).ConfigureAwait(false);
                return resolved ? Results.Ok() : Results.NotFound();
            })
            .WithName("ResolveAlertFiring")
            .WithSummary("Resolve a firing or acknowledged alert");

        return endpoints;
    }
}

// =============================================================================
// Request DTOs
// =============================================================================

/// <summary>Request body for creating an alert rule.</summary>
public sealed record CreateAlertRuleRequest(
    string? ProjectId,
    string? Name,
    string? RuleType,
    string? ConditionJson,
    string? TargetType,
    string? Description = null,
    string? ThresholdJson = null,
    string? TargetFilterJson = null,
    string? Severity = null,
    int? CooldownSeconds = null,
    string? NotificationChannelsJson = null);

/// <summary>Request body for enabling/disabling a rule.</summary>
public sealed record RuleEnabledUpdate(bool Enabled);

/// <summary>Request body for acknowledging an alert firing.</summary>
public sealed record AcknowledgeRequest(string? AcknowledgedBy);
