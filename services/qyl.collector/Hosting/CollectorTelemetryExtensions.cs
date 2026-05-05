using Microsoft.Extensions.Diagnostics.HealthChecks;
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

        return services;
    }
}
