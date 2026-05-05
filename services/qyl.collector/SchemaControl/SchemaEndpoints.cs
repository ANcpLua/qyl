namespace Qyl.Collector.SchemaControl;

public static class SchemaEndpoints
{
    [QylMapEndpoints]
    public static void MapSchemaEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/schema/promotions", static async Task<IResult> (
            PromotionRequest request, SchemaPlanner planner, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.TargetTable))
                return TypedResults.BadRequest(new { error = "TargetTable is required" });

            if (string.IsNullOrWhiteSpace(request.ChangeType))
                return TypedResults.BadRequest(new { error = "ChangeType is required" });

            var promotion = await planner.PlanPromotionAsync(request, ct);
            return TypedResults.Created($"/api/v1/schema/promotions/{promotion.PromotionId}", promotion);
        });

        app.MapGet("/api/v1/schema/promotions", static async (
            SchemaPlanner planner, CancellationToken ct) =>
        {
            var promotions = await planner.GetPendingPromotionsAsync(ct);
            return TypedResults.Ok(promotions);
        });

        app.MapGet("/api/v1/schema/promotions/{promotionId}", static async Task<IResult> (
            string promotionId, SchemaPlanner planner, CancellationToken ct) =>
        {
            var promotion = await planner.GetPromotionAsync(promotionId, ct);
            return promotion is null ? TypedResults.NotFound() : TypedResults.Ok(promotion);
        });

        app.MapPost("/api/v1/schema/promotions/{promotionId}/apply", static async Task<IResult> (
            string promotionId, SchemaExecutor executor, CancellationToken ct) =>
        {
            var result = await executor.ExecutePromotionAsync(promotionId, ct);
            return result is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(result);
        });
    }
}
