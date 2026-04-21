using Qyl.Collector.Dashboards;

namespace Qyl.Collector.Hosting;

public static class CollectorFeatureExtensions
{
    public static IServiceCollection AddQylCollectorFeatures(
        this IServiceCollection services,
        IConfiguration config)
    {
        // All 14 feature services (Identity, Provisioning, Analytics, Errors, Search)
        // auto-register via [QylService(Singleton)] through QylGeneratedRegistry.
        // Only parameterised wiring (HttpClient, dashboard generator) stays here.
        services.AddHttpClient("GitHub", client =>
        {
            client.BaseAddress = new Uri(config.GetValue("GitHub:BaseAddress", "https://api.github.com/") ??
                                         "https://api.github.com/");
            client.DefaultRequestHeaders.Add("User-Agent", "qyl/1.0");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
        }).AddStandardResilienceHandler();

        services.AddDashboardServices();

        return services;
    }
}
