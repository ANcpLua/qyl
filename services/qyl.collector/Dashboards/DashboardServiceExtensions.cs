namespace Qyl.Collector.Dashboards;

public static class DashboardServiceExtensions
{
    public static IServiceCollection AddDashboardServices(this IServiceCollection services)
    {
        services.AddSingleton<DashboardDetector>();
        services.AddSingleton<DashboardService>();
        services.AddHostedService(static sp => sp.GetRequiredService<DashboardService>());
        return services;
    }

    [QylMapEndpoints]
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder endpoints)
    {
        DashboardEndpoints.MapDashboardEndpoints(endpoints);
        return endpoints;
    }
}
