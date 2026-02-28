using System.Diagnostics;
using System.Reflection;

namespace qyl.collector.Health;

/// <summary>
///     Service for gathering detailed health information for the UI dashboard.
/// </summary>
public sealed class HealthUiService(DuckDbStore store, SpanRingBuffer ringBuffer, IConfiguration configuration)
{
    private static readonly DateTimeOffset StartTime = TimeProvider.System.GetUtcNow();
    private static readonly string Version = GetVersion();

    private readonly string _dataPath = configuration["QYL_DATA_PATH"] ?? "qyl.duckdb";

    public async Task<HealthUiResponse> GetHealthAsync(CancellationToken ct = default)
    {
        var now = TimeProvider.System.GetUtcNow();

        // Get latest span first — used by both ingestion health and last-ingestion-time
        var latest = ringBuffer.GetLatest(1, out _);

        var components = new List<ComponentHealth>
        {
            await GetDuckDbHealthAsync(ct).ConfigureAwait(false),
            GetDiskHealth(),
            GetMemoryHealth(),
            GetIngestionHealth(latest)
        };

        var overallStatus = DetermineOverallStatus(components);

        string? lastIngestionTime = latest.Length > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(latest[0].StartTimeUnixNano / 1_000_000).ToString("o")
            : null;

        return new HealthUiResponse
        {
            Status = overallStatus,
            Components = components,
            UptimeSeconds = (long)(now - StartTime).TotalSeconds,
            Version = Version,
            LastIngestionTime = lastIngestionTime,
            CheckedAt = now.ToString("o")
        };
    }

    private async Task<ComponentHealth> GetDuckDbHealthAsync(CancellationToken ct)
    {
        try
        {
            var stats = await store.GetStorageStatsAsync(ct).ConfigureAwait(false);
            var sizeBytes = store.GetStorageSizeBytes();

            return new ComponentHealth
            {
                Name = "duckdb",
                Status = HealthStatus.Healthy,
                Message = "Database connection is healthy",
                Data = new Dictionary<string, object>
                {
                    ["spanCount"] = stats.SpanCount,
                    ["sessionCount"] = stats.SessionCount,
                    ["logCount"] = stats.LogCount,
                    ["storageSizeBytes"] = sizeBytes,
                    ["storageSizeMb"] = Math.Round(sizeBytes / (1024.0 * 1024.0), 2)
                }
            };
        }
        catch (Exception ex)
        {
            return new ComponentHealth
            {
                Name = "duckdb",
                Status = HealthStatus.Unhealthy,
                Message = $"Database connection failed: {ex.Message}"
            };
        }
    }

    private ComponentHealth GetDiskHealth()
    {
        try
        {
            var path = Path.GetFullPath(_dataPath);
            var directory = Path.GetDirectoryName(path) ?? ".";
            var driveInfo = new DriveInfo(Path.GetPathRoot(directory) ?? directory);

            var totalBytes = driveInfo.TotalSize;
            var freeBytes = driveInfo.AvailableFreeSpace;
            var usedPercent = totalBytes > 0
                ? Math.Round((1.0 - ((double)freeBytes / totalBytes)) * 100, 1)
                : 0;

            var status = usedPercent switch
            {
                >= 95 => HealthStatus.Unhealthy,
                >= 85 => HealthStatus.Degraded,
                _ => HealthStatus.Healthy
            };

            var message = status switch
            {
                HealthStatus.Unhealthy => $"Disk space critical: {usedPercent}% used",
                HealthStatus.Degraded => $"Disk space low: {usedPercent}% used",
                _ => $"Disk space OK: {usedPercent}% used"
            };

            return new ComponentHealth
            {
                Name = "disk",
                Status = status,
                Message = message,
                Data = new Dictionary<string, object>
                {
                    ["totalBytes"] = totalBytes,
                    ["freeBytes"] = freeBytes,
                    ["usedPercent"] = usedPercent,
                    ["totalGb"] = Math.Round(totalBytes / (1024.0 * 1024.0 * 1024.0), 2),
                    ["freeGb"] = Math.Round(freeBytes / (1024.0 * 1024.0 * 1024.0), 2)
                }
            };
        }
        catch (Exception ex)
        {
            return new ComponentHealth
            {
                Name = "disk",
                Status = HealthStatus.Degraded,
                Message = $"Could not determine disk space: {ex.Message}"
            };
        }
    }

    private static ComponentHealth GetMemoryHealth()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var workingSetBytes = process.WorkingSet64;
            var privateMemoryBytes = process.PrivateMemorySize64;

            var gcInfo = GC.GetGCMemoryInfo();
            var heapSizeBytes = gcInfo.HeapSizeBytes;
            var totalAvailableBytes = gcInfo.TotalAvailableMemoryBytes;

            var memoryPressure = totalAvailableBytes > 0
                ? Math.Round((double)heapSizeBytes / totalAvailableBytes * 100, 1)
                : 0;

            var status = memoryPressure switch
            {
                >= 90 => HealthStatus.Unhealthy,
                >= 75 => HealthStatus.Degraded,
                _ => HealthStatus.Healthy
            };

            var message = status switch
            {
                HealthStatus.Unhealthy => $"Memory pressure critical: {memoryPressure}%",
                HealthStatus.Degraded => $"Memory pressure elevated: {memoryPressure}%",
                _ => $"Memory usage OK: {memoryPressure}%"
            };

            return new ComponentHealth
            {
                Name = "memory",
                Status = status,
                Message = message,
                Data = new Dictionary<string, object>
                {
                    ["workingSetMb"] = Math.Round(workingSetBytes / (1024.0 * 1024.0), 2),
                    ["privateMemoryMb"] = Math.Round(privateMemoryBytes / (1024.0 * 1024.0), 2),
                    ["heapSizeMb"] = Math.Round(heapSizeBytes / (1024.0 * 1024.0), 2),
                    ["totalAvailableMb"] = Math.Round(totalAvailableBytes / (1024.0 * 1024.0), 2),
                    ["memoryPressurePercent"] = memoryPressure,
                    ["gcGen0Collections"] = GC.CollectionCount(0),
                    ["gcGen1Collections"] = GC.CollectionCount(1),
                    ["gcGen2Collections"] = GC.CollectionCount(2)
                }
            };
        }
        catch (Exception ex)
        {
            return new ComponentHealth
            {
                Name = "memory",
                Status = HealthStatus.Degraded,
                Message = $"Could not determine memory usage: {ex.Message}"
            };
        }
    }

    private ComponentHealth GetIngestionHealth(SpanRecord[] latest)
    {
        var bufferCount = ringBuffer.Count;
        var bufferCapacity = ringBuffer.Capacity;
        var fillPercent = Math.Round((double)bufferCount / bufferCapacity * 100, 1);

        var hasRecentData = false;
        var secondsSinceLastIngestion = -1L;

        if (latest.Length > 0)
        {
            var nanos = latest[0].StartTimeUnixNano;
            var ms = nanos / 1_000_000;
            var lastTime = DateTimeOffset.FromUnixTimeMilliseconds(ms);
            var now = TimeProvider.System.GetUtcNow();
            secondsSinceLastIngestion = (long)(now - lastTime).TotalSeconds;
            hasRecentData = secondsSinceLastIngestion < 300; // 5 minutes
        }

        var status = hasRecentData || bufferCount is 0 ? HealthStatus.Healthy : HealthStatus.Degraded;
        var message = bufferCount is 0
            ? "No data ingested yet"
            : hasRecentData
                ? $"Ingestion active, buffer {fillPercent}% full"
                : $"No recent ingestion (last: {secondsSinceLastIngestion}s ago)";

        var data = new Dictionary<string, object>
        {
            ["bufferCount"] = bufferCount,
            ["bufferCapacity"] = bufferCapacity,
            ["bufferFillPercent"] = fillPercent,
            ["generation"] = ringBuffer.Generation
        };

        if (secondsSinceLastIngestion >= 0)
        {
            data["secondsSinceLastIngestion"] = secondsSinceLastIngestion;
        }

        return new ComponentHealth { Name = "ingestion", Status = status, Message = message, Data = data };
    }

    private static HealthStatus DetermineOverallStatus(IReadOnlyList<ComponentHealth> components)
    {
        if (components.Any(static c => c.Status == HealthStatus.Unhealthy))
            return HealthStatus.Unhealthy;
        if (components.Any(static c => c.Status == HealthStatus.Degraded))
            return HealthStatus.Degraded;
        return HealthStatus.Healthy;
    }

    private static string GetVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? assembly.GetName().Version?.ToString()
                      ?? "unknown";

        var plusIndex = version.IndexOf('+');
        return plusIndex > 0 ? version[..plusIndex] : version;
    }
}
