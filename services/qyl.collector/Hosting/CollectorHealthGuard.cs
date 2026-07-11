using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Qyl.Collector.Hosting;

/// <summary>
/// Boot-time assertion that the collector's health surface is actually wired.
/// </summary>
/// <remarks>
/// The <c>/health</c> and <c>/alive</c> endpoints (and the generated health-check registration)
/// are wired by the qyl.instrumentation source generator's <c>Build()</c> interceptor. Interceptor
/// wiring can go silently inert — another generator claiming the same call site, a refactor of the
/// <c>Build()</c> expression — and a collector without a real health endpoint lets the SPA
/// fallback answer 200 for <c>/health</c>, fooling Railway's <c>healthcheckPath</c> and
/// Qyl.Run's readiness probe into deploying/announcing a broken instance. Fail the boot loudly
/// instead.
/// </remarks>
internal static class CollectorHealthGuard
{
    public static void ThrowIfHealthSurfaceUnwired(WebApplication app)
    {
        var mappedRoutes = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(static source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(static endpoint => endpoint.RoutePattern.RawText)
            .Where(static pattern => pattern is not null)
            .ToHashSet(StringComparer.Ordinal);

        if (!mappedRoutes.Contains(QylEndpoints.Health) || !mappedRoutes.Contains(QylEndpoints.Alive))
        {
            throw new InvalidOperationException(
                $"The collector booted without a mapped '{QylEndpoints.Health}'/'{QylEndpoints.Alive}' endpoint. " +
                "The qyl.instrumentation Build() interceptor (MapQylDefaultEndpoints) did not run — without it the " +
                "SPA fallback would answer 200 for /health and fake a healthy instance. Restore the interceptor " +
                "wiring or map the health endpoints explicitly.");
        }

        // "duckdb" is the name declared by [QylHealthCheck] on DuckDbHealthCheck; the generated
        // registry registers it under exactly that name.
        var registrations = app.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;
        if (!registrations.Any(static registration => registration.Name is "duckdb"))
        {
            throw new InvalidOperationException(
                "The collector booted without the 'duckdb' health check registered; /health would report healthy " +
                "without ever touching storage. The generated RegisterQylHealthChecks call did not run.");
        }
    }
}
