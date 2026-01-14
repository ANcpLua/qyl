using Microsoft.Extensions.Diagnostics.HealthChecks;
using qyl.collector.Storage;

namespace qyl.collector.Health;

/// <summary>
///     Health check that verifies DuckDB database connectivity.
///     Executes a simple query to validate the connection is alive.
/// </summary>
public sealed class DuckDbHealthCheck(DuckDbStore store) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = await store.GetStorageStatsAsync(cancellationToken).ConfigureAwait(false);

            return HealthCheckResult.Healthy(
                description: "DuckDB connection is healthy",
                data: new Dictionary<string, object>
                {
                    ["span_count"] = stats.SpanCount,
                    ["session_count"] = stats.SessionCount
                });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                description: "DuckDB connection failed",
                exception: ex);
        }
    }
}
