namespace Qyl.Collector.Retention;

internal sealed record RetentionOptions
{
    private const long BytesPerMegabyte = 1024L * 1024L;

    public int Days { get; init; } = 30;

    public TimeSpan Interval { get; init; } = TimeSpan.FromMinutes(60);

    public long StorageMinimumFreeBytes { get; init; } = 2048L * BytesPerMegabyte;

    public bool IsEnabled => Days > 0;

    public static RetentionOptions FromConfiguration(IConfiguration configuration)
    {
        var days = int.Parse(configuration["QYL_RETENTION_DAYS"] ?? "30", CultureInfo.InvariantCulture);
        var intervalMinutes = int.Parse(
            configuration["QYL_RETENTION_INTERVAL_MINUTES"] ?? "60",
            CultureInfo.InvariantCulture);
        var minimumFreeMegabytes = long.Parse(
            configuration["QYL_STORAGE_MIN_FREE_MB"] ?? "2048",
            CultureInfo.InvariantCulture);

        if (days < 0)
            throw new ArgumentOutOfRangeException(nameof(configuration), days, "Retention days cannot be negative.");
        if (intervalMinutes < 1)
            throw new ArgumentOutOfRangeException(nameof(configuration), intervalMinutes, "Retention interval must be positive.");
        if (minimumFreeMegabytes < 0)
            throw new ArgumentOutOfRangeException(nameof(configuration), minimumFreeMegabytes, "Minimum free space cannot be negative.");

        return new RetentionOptions
        {
            Days = days,
            Interval = TimeSpan.FromMinutes(intervalMinutes),
            StorageMinimumFreeBytes = checked(minimumFreeMegabytes * BytesPerMegabyte)
        };
    }
}
