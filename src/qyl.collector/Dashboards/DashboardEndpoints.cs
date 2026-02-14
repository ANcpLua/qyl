namespace qyl.collector.Dashboards;

/// <summary>
///     Maps dashboard REST API endpoints.
/// </summary>
public static class DashboardEndpoints
{
    internal static void MapDashboardEndpoints(IEndpointRouteBuilder endpoints)
    {
        // List all detected dashboards (only available ones)
        endpoints.MapGet("/api/v1/dashboards", ([Microsoft.AspNetCore.Mvc.FromServices] DashboardService service) =>
        {
            var dashboards = service.GetAvailable()
                .Where(d => d.IsAvailable)
                .ToList();
            return Results.Ok(new { items = dashboards, total = dashboards.Count });
        });

        // Get dashboard data with computed widgets
        endpoints.MapGet("/api/v1/dashboards/{id}", async (
            string id,
            [Microsoft.AspNetCore.Mvc.FromServices] DashboardService service,
            DuckDbStore store,
            CancellationToken ct) =>
        {
            var dashboard = service.GetAvailable().FirstOrDefault(d => d.Id == id);
            if (dashboard is null || !dashboard.IsAvailable)
                return Results.NotFound();

            await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
            var widgets = await DashboardQueries.GetWidgetsAsync(id, lease.Connection, ct).ConfigureAwait(false);

            return Results.Ok(new DashboardData(
                dashboard.Id,
                dashboard.Title,
                dashboard.Description,
                dashboard.Icon,
                widgets));
        });

        // Get individual widget data (partial refresh)
        endpoints.MapGet("/api/v1/dashboards/{id}/widgets", async (
            string id,
            [Microsoft.AspNetCore.Mvc.FromServices] DashboardService service,
            DuckDbStore store,
            CancellationToken ct) =>
        {
            var dashboard = service.GetAvailable().FirstOrDefault(d => d.Id == id);
            if (dashboard is null || !dashboard.IsAvailable)
                return Results.NotFound();

            await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
            var widgets = await DashboardQueries.GetWidgetsAsync(id, lease.Connection, ct).ConfigureAwait(false);

            return Results.Ok(new { widgets });
        });
    }
}
