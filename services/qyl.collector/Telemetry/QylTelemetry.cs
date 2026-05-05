
namespace Qyl.Collector.Telemetry;

public static class QylTelemetry
{
    public const string ServiceName = "Qyl.Collector";
    public const string ServiceVersion = "1.0.0";


    public static readonly ActivitySource Source = new(new ActivitySourceOptions(ServiceName)
    {
        Version = ServiceVersion,
        TelemetrySchemaUrl = SchemaVersion.Current.ToSchemaUrl().ToString()
    });


    public static readonly Meter Meter = new(new MeterOptions(ServiceName)
    {
        Version = ServiceVersion, TelemetrySchemaUrl = SchemaVersion.Current.ToSchemaUrl().ToString()
    });
}

public static class QylMetrics
{
    private static Func<long>? s_storageSizeCallback;

    public static readonly ObservableGauge<long> StorageSize =
        QylTelemetry.Meter.CreateObservableGauge(
            QylAttr.Storage.Size,
            GetStorageSizeBytes,
            "By",
            "Approximate storage size in bytes");

    public static void RegisterStorageSizeCallback(Func<long> callback) =>
        s_storageSizeCallback = callback;

    private static long GetStorageSizeBytes() =>
        s_storageSizeCallback?.Invoke() ?? 0;
}
