namespace qyl.collector.Storage;

/// <summary>
///     Configuration for telemetry data retention and cleanup limits.
/// </summary>
public sealed class TelemetryLimitsOptions
{
    /// <summary>
    ///     Maximum number of days to retain telemetry data. Default: 30 days.
    /// </summary>
    public int MaxRetentionDays { get; set; } = 30;

    /// <summary>
    ///     Maximum number of spans to retain. Cleanup triggers when exceeded. Default: 1,000,000.
    /// </summary>
    public int MaxSpanCount { get; set; } = 1_000_000;

    /// <summary>
    ///     Target span count after cleanup. Default: 900,000 (90% of max).
    /// </summary>
    public int TargetSpanCount { get; set; } = 900_000;

    /// <summary>
    ///     Maximum number of log records to retain. Default: 500,000.
    /// </summary>
    public int MaxLogCount { get; set; } = 500_000;

    /// <summary>
    ///     Target log count after cleanup. Default: 450,000 (90% of max).
    /// </summary>
    public int TargetLogCount { get; set; } = 450_000;

    /// <summary>
    ///     Interval between cleanup runs. Default: 5 minutes.
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     Whether to archive data to Parquet before deletion. Default: false.
    /// </summary>
    public bool EnableArchive { get; set; }

    /// <summary>
    ///     Directory for archived Parquet files. Required if EnableArchive is true.
    /// </summary>
    public string? ArchiveDirectory { get; set; }
}
