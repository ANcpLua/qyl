
namespace Qyl.Collector.Telemetry;

internal static class QylTelemetry
{
    public const string ServiceName = "Qyl.Collector";

    public static readonly ActivitySource Source = new(new ActivitySourceOptions(ServiceName)
    {
        Version = BuildVersion.InformationalVersion,
        TelemetrySchemaUrl = CollectorSemanticAttributeCatalog.SchemaUrlCurrent
    });
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
