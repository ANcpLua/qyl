using Microsoft.AspNetCore.Mvc;

namespace Qyl.Collector.Dashboards;

public static class DashboardEndpoints
{
    internal static void MapDashboardEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/dashboards", ([FromServices] DashboardService service) =>
        {
            var dashboards = service.GetAvailable()
                .Where(d => d.IsAvailable)
                .ToList();
            return TypedResults.Ok(new { items = dashboards, total = dashboards.Count });
        });

        endpoints.MapGet("/api/v1/dashboards/{id}", async Task<IResult> (
            string id,
            [FromServices] DashboardService service,
            DuckDbStore store,
            CancellationToken ct) =>
        {
            var dashboard = service.GetAvailable().FirstOrDefault(d => d.Id == id);
            if (dashboard is null || !dashboard.IsAvailable)
                return TypedResults.NotFound();

            var widgets = await store.ExecuteReadAsync(con => DashboardQueries.GetWidgets(id, con), ct).ConfigureAwait(false);

            return TypedResults.Ok(new DashboardData(
                dashboard.Id,
                dashboard.Title,
                dashboard.Description,
                dashboard.Icon,
                widgets));
        });

        endpoints.MapGet("/api/v1/dashboards/{id}/widgets", async Task<IResult> (
            string id,
            [FromServices] DashboardService service,
            DuckDbStore store,
            CancellationToken ct) =>
        {
            var dashboard = service.GetAvailable().FirstOrDefault(d => d.Id == id);
            if (dashboard is null || !dashboard.IsAvailable)
                return TypedResults.NotFound();

            var widgets = await store.ExecuteReadAsync(con => DashboardQueries.GetWidgets(id, con), ct).ConfigureAwait(false);

            return TypedResults.Ok(new { widgets });
        });
    }
}
