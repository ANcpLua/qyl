namespace qyl.collector.Health;

/// <summary>
///     Extension methods for configuring qyl health checks.
/// </summary>
public static class HealthExtensions
{
    /// <summary>
    ///     Adds comprehensive qyl health checks including DuckDB connectivity,
    ///     resource monitoring, and telemetry publishing.
    /// </summary>
    public static IServiceCollection AddQylHealthChecks(this IServiceCollection services)
    {
        // Health checks with tags for K8s probes
        var builder = services.AddHealthChecks()
            .AddCheck<DuckDbHealthCheck>(
                name: "duckdb",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["db", "storage", "ready"])
            .AddApplicationLifecycleHealthCheck(tags: ["live"]);

        // Resource monitoring only available on Linux (cgroups) and Windows
        // macOS has no ISnapshotProvider implementation
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            builder.AddResourceUtilizationHealthCheck(static options =>
            {
                options.CpuThresholds = new ResourceUsageThresholds
                {
                    DegradedUtilizationPercentage = 80, UnhealthyUtilizationPercentage = 95
                };
                options.MemoryThresholds = new ResourceUsageThresholds
                {
                    DegradedUtilizationPercentage = 85, UnhealthyUtilizationPercentage = 95
                };
            }, tags: ["resources", "live"]);

            // Container-aware resource monitoring (CPU/memory metrics)
            services.AddResourceMonitoring();
        }

        // Publish health status as OTel metrics
        services.AddTelemetryHealthCheckPublisher();

        return services;
    }
}
