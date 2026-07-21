using Microsoft.Extensions.Diagnostics.HealthChecks;
using Qyl.Collector.Retention;

namespace Qyl.Collector.Health;

internal sealed class DuckDbHealthCheck(IQylStore store, RetentionOptions retentionOptions) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = await store.GetStorageStatsAsync(ProjectScope.DefaultProjectId, ct: cancellationToken)
                .ConfigureAwait(false);
            var fileMetrics = store.GetStorageFileMetrics();
            var data = new Dictionary<string, object>
            {
                ["span_count"] = stats.SpanCount,
                ["session_count"] = stats.SessionCount,
                ["database_file_size_bytes"] = fileMetrics.DatabaseFileSizeBytes,
                ["storage_free_bytes"] = fileMetrics.StorageFreeBytes
            };

            if (fileMetrics.StorageFreeBytes < retentionOptions.StorageMinimumFreeBytes)
            {
                return HealthCheckResult.Degraded(
                    $"DuckDB storage free space is below the configured minimum; " +
                    $"database_file_size_bytes={fileMetrics.DatabaseFileSizeBytes}; " +
                    $"storage_free_bytes={fileMetrics.StorageFreeBytes}",
                    data: data);
            }

            return HealthCheckResult.Healthy(
                $"DuckDB connection is healthy; database_file_size_bytes={fileMetrics.DatabaseFileSizeBytes}; " +
                $"storage_free_bytes={fileMetrics.StorageFreeBytes}",
                data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "DuckDB connection failed",
                ex);
        }
    }
}
