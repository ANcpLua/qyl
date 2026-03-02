using System.Reflection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MsHealthStatus = Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus;

namespace qyl.collector.Health;

/// <summary>
///     2-liner health infrastructure for qyl.collector.
///     <code>
///     services.AddQylHealthChecks();
///     app.MapQylHealthChecks();
///     </code>
/// </summary>
public static class HealthExtensions
{
    internal static readonly DateTimeOffset StartTime = TimeProvider.System.GetUtcNow();

    internal static readonly SemVer AppVersion = GetVersion();

    // ── IServiceCollection: DI registration ────────────────────────────────

    /// <summary>
    ///     Registers DuckDB health check, resource monitoring, OTel publisher, and HealthUiService.
    /// </summary>
    public static IServiceCollection AddQylHealthChecks(this IServiceCollection services)
    {
        services.AddSingleton<HealthUiService>();

        var builder = services.AddHealthChecks()
            .AddCheck<DuckDbHealthCheck>(
                "duckdb",
                MsHealthStatus.Unhealthy,
                ["db", "storage", "ready"])
            .AddApplicationLifecycleHealthCheck("live");

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
            }, "resources", "live");

            services.AddResourceMonitoring();
        }

        services.AddTelemetryHealthCheckPublisher();

        return services;
    }

    // ── IEndpointRouteBuilder: route registration ──────────────────────────

    /// <summary>
    ///     Maps /alive, /health, /ready (K8s probes) and /health/ui (dashboard).
    /// </summary>
    public static IEndpointRouteBuilder MapQylHealthChecks(this IEndpointRouteBuilder app)
    {
        var health = app.MapGroup("")
            .AddEndpointFilter(static async (context, next) =>
            {
                var result = await next(context);
                context.HttpContext.Response.Headers.CacheControl = "no-store";
                context.HttpContext.Response.Headers["X-Content-Type-Options"] = "nosniff";
                return result;
            });

        health.MapGet("/alive", RunProbe("live")).WithName("LivenessCheck");
        health.MapGet("/health", RunProbe("ready")).WithName("HealthCheck");
        health.MapGet("/ready", RunProbe("ready")).WithName("ReadyCheck");

        health.MapGet("/health/ui", static async (HealthUiService healthUi, CancellationToken ct) =>
            Results.Ok(await healthUi.GetHealthAsync(ct).ConfigureAwait(false)));

        return app;
    }

    // ── Private ────────────────────────────────────────────────────────────

    private static Func<IServiceProvider, CancellationToken, Task<IResult>> RunProbe(string tag) =>
        async (sp, ct) =>
        {
            var now = TimeProvider.System.GetUtcNow();
            var uptimeSeconds = (long)(now - StartTime).TotalSeconds;

            if (sp.GetService<HealthCheckService>() is not { } healthService)
                return Results.Ok(new HealthResponse
                {
                    Status = Qyl.Models.HealthStatus.Healthy,
                    Version = AppVersion,
                    UptimeSeconds = uptimeSeconds,
                });

            var result = await healthService
                .CheckHealthAsync(c => c.Tags.Contains(tag), ct)
                .ConfigureAwait(false);

            var status = result.Status switch
            {
                MsHealthStatus.Healthy => Qyl.Models.HealthStatus.Healthy,
                MsHealthStatus.Degraded => Qyl.Models.HealthStatus.Degraded,
                _ => Qyl.Models.HealthStatus.Unhealthy,
            };

            var response = new HealthResponse
            {
                Status = status,
                Version = AppVersion,
                UptimeSeconds = uptimeSeconds,
                Components = result.Entries.Count > 0
                    ? result.Entries.ToDictionary(
                        e => e.Key,
                        e => (object)new
                        {
                            status = e.Value.Status.ToString().ToLowerInvariant(),
                            description = e.Value.Description,
                            data = e.Value.Data.Count > 0 ? e.Value.Data : null,
                        })
                    : null,
            };

            return result.Status == MsHealthStatus.Healthy
                ? Results.Ok(response)
                : Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable);
        };

    private static SemVer GetVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? assembly.GetName().Version?.ToString()
                      ?? "0.0.0";

        var plusIndex = version.IndexOf('+');
        return plusIndex > 0 ? version[..plusIndex] : version;
    }
}
