using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace qyl.collector.Health;

/// <summary>
///     Health check that verifies DuckDB database connectivity.
///     Executes a simple query to validate the connection is alive.
/// </summary>
public sealed class DuckDbHealthCheck(DuckDbStore store) : IHealthCheck
{
    private readonly DuckDbStore _store = store;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = await _store.GetStorageStatsAsync(cancellationToken).ConfigureAwait(false);

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
