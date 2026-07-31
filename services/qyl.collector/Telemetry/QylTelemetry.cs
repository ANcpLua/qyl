
using System.Diagnostics.Metrics;

namespace Qyl.Collector.Telemetry;

internal static class QylTelemetry
{
    public const string ServiceName = "Qyl.Collector";

    private static readonly Meter s_meter = new(
        ServiceName,
        BuildVersion.InformationalVersion);
    private static readonly Counter<long> s_workflowLifecycleOutcomes =
        s_meter.CreateCounter<long>(
            "qyl.workflow.lifecycle.outcomes",
            "{outcome}");

    public static readonly ActivitySource Source = new(new ActivitySourceOptions(ServiceName)
    {
        Version = BuildVersion.InformationalVersion,
        TelemetrySchemaUrl = CollectorSemanticAttributeCatalog.SchemaUrlCurrent
    });

    public static void RecordWorkflowLifecycleOutcome(
        string outcome,
        string reason) =>
        s_workflowLifecycleOutcomes.Add(
            1,
            new KeyValuePair<string, object?>("outcome", outcome),
            new KeyValuePair<string, object?>("reason", reason));
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
