
using OtelSchemaUrl = Qyl.OpenTelemetry.SemanticConventions.SchemaUrl;

namespace Qyl.Collector.Telemetry;

public static class QylTelemetry
{
    public const string ServiceName = "Qyl.Collector";
    public const string ServiceVersion = "1.0.0";
    public const string StorageMeterName = "Qyl.Collector.storage";


    public static readonly ActivitySource Source = new(new ActivitySourceOptions(ServiceName)
    {
        Version = ServiceVersion,
        TelemetrySchemaUrl = OtelSchemaUrl.Current
    });


    public static readonly Meter Meter = new(new MeterOptions(ServiceName)
    {
        Version = ServiceVersion, TelemetrySchemaUrl = OtelSchemaUrl.Current
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
        System.Threading.Volatile.Write(ref s_storageSizeCallback, Guard.NotNull(callback));

    private static long GetStorageSizeBytes()
    {
        var callback = System.Threading.Volatile.Read(ref s_storageSizeCallback);
        if (callback is null)
            return 0;

        try
        {
            return Math.Max(0, callback());
        }
        catch
        {
            return 0;
        }
    }
}
