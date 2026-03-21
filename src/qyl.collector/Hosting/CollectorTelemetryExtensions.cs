using Qyl.Collector.Analytics;
using Qyl.Collector.Health;
using Qyl.Collector.Insights;
using Qyl.Collector.Services;
using Qyl.Collector.Telemetry;

namespace Qyl.Collector.Hosting;

public static class CollectorTelemetryExtensions
{
    public static IServiceCollection AddQylCollectorTelemetry(
        this IServiceCollection services,
        IHostEnvironment environment)
    {
        services.AddQylTelemetry();
        services.AddLogging(logging => logging.AddQylLogging(environment));
        services.AddQylHealthChecks();

        services.AddHostedService<InsightsMaterializerService>();
        services.AddHostedService<ServiceMaterializerService>();
        services.AddHostedService<EmbeddingClusterWorker>();

        return services;
    }
}
