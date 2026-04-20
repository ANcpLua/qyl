namespace Qyl.Collector.Hosting;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Telemetry;

public static class CollectorTelemetryExtensions
{
    public static IServiceCollection AddQylCollectorTelemetry(
        this IServiceCollection services,
        IHostEnvironment environment)
    {
        services.AddQylTelemetry();
        services.AddLogging(logging => logging.AddQylLogging(environment));

        // Complex built-in checks that don't map to a simple [QylHealthCheck] class tag
        // (they're parameterised extension calls on IHealthChecksBuilder).
        var healthBuilder = services.AddHealthChecks()
            .AddApplicationLifecycleHealthCheck(QylEndpoints.LiveTag);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            healthBuilder.AddResourceUtilizationHealthCheck(static options =>
            {
                options.CpuThresholds = new ResourceUsageThresholds
                {
                    DegradedUtilizationPercentage = 80, UnhealthyUtilizationPercentage = 95
                };
                options.MemoryThresholds = new ResourceUsageThresholds
                {
                    DegradedUtilizationPercentage = 85, UnhealthyUtilizationPercentage = 95
                };
            }, "resources", QylEndpoints.LiveTag);

            services.AddResourceMonitoring();
        }

        services.AddTelemetryHealthCheckPublisher();

        // The rest auto-wires via the generator:
        //   [QylHostedService] -> InsightsMaterializerService, ServiceMaterializerService,
        //                         EmbeddingClusterWorker
        //   [QylHealthCheck]   -> DuckDbHealthCheck ("duckdb", [db, storage, ready])
        //   [QylService]       -> HealthUiService (Singleton)
        return services;
    }
}
