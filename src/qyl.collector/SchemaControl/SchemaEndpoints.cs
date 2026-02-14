namespace qyl.collector.SchemaControl;

/// <summary>
///     REST endpoints for schema promotion and migration management.
/// </summary>
public static class SchemaEndpoints
{
    public static void MapSchemaEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/schema/promotions", static async (
            PromotionRequest request, SchemaPlanner planner, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.TargetTable))
                return Results.BadRequest(new { error = "TargetTable is required" });

            if (string.IsNullOrWhiteSpace(request.ChangeType))
                return Results.BadRequest(new { error = "ChangeType is required" });

            var promotion = await planner.PlanPromotionAsync(request, ct);
            return Results.Created($"/api/v1/schema/promotions/{promotion.PromotionId}", promotion);
        });

        app.MapGet("/api/v1/schema/promotions", static async (
            SchemaPlanner planner, CancellationToken ct) =>
        {
            var promotions = await planner.GetPendingPromotionsAsync(ct);
            return Results.Ok(promotions);
        });

        app.MapGet("/api/v1/schema/promotions/{promotionId}", static async (
            string promotionId, SchemaPlanner planner, CancellationToken ct) =>
        {
            var promotion = await planner.GetPromotionAsync(promotionId, ct);
            return promotion is null ? Results.NotFound() : Results.Ok(promotion);
        });

        app.MapPost("/api/v1/schema/promotions/{promotionId}/apply", static async (
            string promotionId, SchemaExecutor executor, CancellationToken ct) =>
        {
            var result = await executor.ExecutePromotionAsync(promotionId, ct);
            return result is null
                ? Results.NotFound()
                : Results.Ok(result);
        });
    }
}
