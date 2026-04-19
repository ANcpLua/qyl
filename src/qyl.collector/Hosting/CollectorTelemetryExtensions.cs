using Microsoft.Extensions.Diagnostics.HealthChecks;
using Qyl.Collector.Health;
using Qyl.Collector.Telemetry;
using MsHealthStatus = Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus;

namespace Qyl.Collector.Hosting;

public static class CollectorTelemetryExtensions
{
    public static IServiceCollection AddQylCollectorTelemetry(
        this IServiceCollection services,
        IHostEnvironment environment)
    {
        services.AddQylTelemetry();
        services.AddLogging(logging => logging.AddQylLogging(environment));

        // Register collector-specific health checks. The self/live and ready probes are already
        // wired by QylServiceDefaults; we just add domain-specific checks under the same tags so
        // /alive and /health pick them up automatically.
        services.AddSingleton<HealthUiService>();

        var healthBuilder = services.AddHealthChecks()
            .AddCheck<DuckDbHealthCheck>(
                "duckdb",
                MsHealthStatus.Unhealthy,
                ["db", "storage", QylEndpoints.ReadyTag])
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

        // Hosted services (InsightsMaterializerService, ServiceMaterializerService,
        // EmbeddingClusterWorker) auto-register via [QylHostedService] — see
        // QylGeneratedRegistry.RegisterQylHostedServices emitted by the generator.
        return services;
    }
}
