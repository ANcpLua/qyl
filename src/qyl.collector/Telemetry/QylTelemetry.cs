// =============================================================================
// qyl Telemetry Infrastructure - .NET 10 ActivitySource + Metrics
// Uses ActivitySourceOptions/MeterOptions with OTel Schema URL (new in .NET 10)
// =============================================================================

namespace qyl.collector.Telemetry;

/// <summary>
///     Central telemetry definitions for qyl.collector using .NET 10 APIs.
/// </summary>
public static class QylTelemetry
{
    public const string ServiceName = "qyl.collector";
    public const string ServiceVersion = "1.0.0";

    // ==========================================================================
    // .NET 10: ActivitySourceOptions with TelemetrySchemaUrl
    // ==========================================================================

    /// <summary>
    ///     ActivitySource for distributed tracing with OTel 1.39 schema URL.
    /// </summary>
    public static readonly ActivitySource Source = new(new ActivitySourceOptions(ServiceName)
    {
        Version = ServiceVersion,
        TelemetrySchemaUrl = SchemaVersion.Current.ToSchemaUrl().ToString() // "https://opentelemetry.io/schemas/1.39.0"
    });

    // ==========================================================================
    // .NET 10: MeterOptions with TelemetrySchemaUrl
    // ==========================================================================

    /// <summary>
    ///     Meter for metrics collection with OTel 1.39 schema URL.
    /// </summary>
    public static readonly Meter Meter = new(new MeterOptions(ServiceName)
    {
        Version = ServiceVersion, TelemetrySchemaUrl = SchemaVersion.Current.ToSchemaUrl().ToString()
    });
}

/// <summary>
///     qyl Metrics using .NET 10 Meter with schema URL.
/// </summary>
public static class QylMetrics
{
    private static Func<long>? s_storageSizeCallback;

    /// <summary>Storage size gauge (approximate).</summary>
    public static readonly ObservableGauge<long> StorageSize =
        QylTelemetry.Meter.CreateObservableGauge(
            "qyl.storage.size",
            GetStorageSizeBytes,
            "By",
            "Approximate storage size in bytes");

    /// <summary>
    ///     Registers the callback for storage size metrics.
    ///     Called during startup after DuckDbStore is instantiated.
    /// </summary>
    public static void RegisterStorageSizeCallback(Func<long> callback) =>
        s_storageSizeCallback = callback;

    private static long GetStorageSizeBytes() =>
        s_storageSizeCallback?.Invoke() ?? 0;
}
