using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Qyl.Collector.Health;

[QylHealthCheck("duckdb", "db", "storage", QylEndpoints.ReadyTag)]
internal sealed class DuckDbHealthCheck(DuckDbStore store) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = await store.GetStorageStatsAsync(ct: cancellationToken).ConfigureAwait(false);

            return HealthCheckResult.Healthy(
                "DuckDB connection is healthy",
                new Dictionary<string, object>
                {
                    ["span_count"] = stats.SpanCount, ["session_count"] = stats.SessionCount
                });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "DuckDB connection failed",
                ex);
        }
    }
}
