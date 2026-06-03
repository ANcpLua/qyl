
namespace Qyl.Collector.Telemetry;

internal static class QylTelemetry
{
    public const string ServiceName = "Qyl.Collector";
    public const string ServiceVersion = "1.0.0";
    public const string StorageMeterName = "Qyl.Collector.storage";


    public static readonly ActivitySource Source = new(new ActivitySourceOptions(ServiceName)
    {
        Version = ServiceVersion,
        TelemetrySchemaUrl = CollectorSemanticAttributeCatalog.SchemaUrlCurrent
    });


    public static readonly Meter Meter = new(new MeterOptions(ServiceName)
    {
        Version = ServiceVersion, TelemetrySchemaUrl = CollectorSemanticAttributeCatalog.SchemaUrlCurrent
    });
}

internal static class QylMetrics
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

internal static class QylLatencyNames
{
    internal static class Checkpoints
    {
        public const string DbQuery = "collector.db_query";
        public const string SpanIngest = "collector.span_ingest";
        public const string SpanStore = "collector.span_store";
        public const string SessionQuery = "collector.session_query";
        public const string GenAiExtract = "collector.genai_extract";
    }

    internal static class Measures
    {
        public const string IngestionDuration = "collector.ingestion_duration";
        public const string QueryDuration = "collector.query_duration";
        public const string StorageDuration = "collector.storage_duration";
    }

    internal static class Tags
    {
        public const string SpanCount = "collector.span_count";
    }
}
