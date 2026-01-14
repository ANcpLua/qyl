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

    // ==========================================================================
    // Helper Methods
    // ==========================================================================

    /// <summary>
    ///     Starts an activity with standard qyl tags.
    /// </summary>
    public static Activity? StartActivity(
        string name,
        ActivityKind kind = ActivityKind.Internal,
        string? sessionId = null)
    {
        var activity = Source.StartActivity(name, kind);

        if (activity is not null && sessionId is not null)
        {
            activity.SetTag("session.id", sessionId);
        }

        return activity;
    }

    /// <summary>
    ///     Adds GenAI-specific tags to an activity.
    /// </summary>
    public static void SetGenAiTags(
        this Activity activity,
        string? provider = null,
        string? model = null,
        string? operation = null,
        long? inputTokens = null,
        long? outputTokens = null)
    {
        if (provider is not null)
            activity.SetTag("gen_ai.provider.name", provider);
        if (model is not null)
            activity.SetTag("gen_ai.request.model", model);
        if (operation is not null)
            activity.SetTag("gen_ai.operation.name", operation);
        if (inputTokens.HasValue)
            activity.SetTag("gen_ai.usage.input_tokens", inputTokens.Value);
        if (outputTokens.HasValue)
            activity.SetTag("gen_ai.usage.output_tokens", outputTokens.Value);
    }

    /// <summary>
    ///     Records a span ingestion event with full details.
    /// </summary>
    public static void RecordIngestionEvent(
        this Activity activity,
        int spanCount,
        bool hasGenAi = false) =>
        activity.AddEvent(new ActivityEvent(
            "spans.ingested",
            tags: new ActivityTagsCollection { ["span.count"] = spanCount, ["has_genai"] = hasGenAi }));

    // ==========================================================================
    // Activity Names (following OTel GenAI semantic conventions)
    // ==========================================================================

    public static class Activities
    {
        // Ingestion
        public const string IngestSpans = "qyl.ingest_spans";
        public const string IngestOtlpJson = "qyl.ingest_otlp_json";
        public const string IngestOtlpGrpc = "qyl.ingest_otlp_grpc";

        // Storage
        public const string StoreSpans = "qyl.store_spans";
        public const string QuerySpans = "qyl.query_spans";
        public const string QuerySessions = "qyl.query_sessions";

        // GenAI specific
        public const string ExtractGenAi = "qyl.extract_genai";
        public const string NormalizeAttributes = "qyl.normalize_attributes";
    }
}

/// <summary>
///     qyl Metrics using .NET 10 Meter with schema URL.
/// </summary>
public static class QylMetrics
{
    // ==========================================================================
    // Storage Size Callback (bridging static metrics with DI-managed store)
    // ==========================================================================

    private static Func<long>? s_storageSizeCallback;

    /// <summary>
    ///     Registers the callback for storage size metrics.
    ///     Called during startup after DuckDbStore is instantiated.
    /// </summary>
    public static void RegisterStorageSizeCallback(Func<long> callback) =>
        s_storageSizeCallback = callback;

    // ==========================================================================
    // Counters
    // ==========================================================================

    /// <summary>Spans ingested counter.</summary>
    public static readonly Counter<long> SpansIngested =
        QylTelemetry.Meter.CreateCounter<long>(
            "qyl.spans.ingested",
            "{span}",
            "Number of spans ingested");

    /// <summary>GenAI spans ingested counter.</summary>
    public static readonly Counter<long> GenAiSpansIngested =
        QylTelemetry.Meter.CreateCounter<long>(
            "qyl.genai_spans.ingested",
            "{span}",
            "Number of GenAI spans ingested");

    /// <summary>Sessions created counter.</summary>
    public static readonly Counter<long> SessionsCreated =
        QylTelemetry.Meter.CreateCounter<long>(
            "qyl.sessions.created",
            "{session}",
            "Number of sessions created");

    /// <summary>Tokens processed counter.</summary>
    public static readonly Counter<long> TokensProcessed =
        QylTelemetry.Meter.CreateCounter<long>(
            "qyl.tokens.processed",
            "{token}",
            "Number of tokens processed (input + output)");

    /// <summary>Ingestion errors counter.</summary>
    public static readonly Counter<long> IngestionErrors =
        QylTelemetry.Meter.CreateCounter<long>(
            "qyl.ingestion.errors",
            "{error}",
            "Number of ingestion errors");

    // ==========================================================================
    // Histograms
    // ==========================================================================

    /// <summary>Span ingestion duration histogram.</summary>
    public static readonly Histogram<double> IngestionDuration =
        QylTelemetry.Meter.CreateHistogram<double>(
            "qyl.ingestion.duration",
            "s",
            "Time to ingest a batch of spans");

    /// <summary>Query duration histogram.</summary>
    public static readonly Histogram<double> QueryDuration =
        QylTelemetry.Meter.CreateHistogram<double>(
            "qyl.query.duration",
            "s",
            "Time to execute a query");

    /// <summary>Batch size histogram.</summary>
    public static readonly Histogram<int> BatchSize =
        QylTelemetry.Meter.CreateHistogram<int>(
            "qyl.batch.size",
            "{span}",
            "Number of spans per ingestion batch");

    // ==========================================================================
    // Gauges (UpDownCounters for current values)
    // ==========================================================================

    /// <summary>Active sessions gauge.</summary>
    public static readonly UpDownCounter<long> ActiveSessions =
        QylTelemetry.Meter.CreateUpDownCounter<long>(
            "qyl.sessions.active",
            "{session}",
            "Number of active sessions");

    /// <summary>Storage size gauge (approximate).</summary>
    public static readonly ObservableGauge<long> StorageSize =
        QylTelemetry.Meter.CreateObservableGauge(
            "qyl.storage.size",
            GetStorageSizeBytes,
            "By",
            "Approximate storage size in bytes");

    // ==========================================================================
    // Helper Methods
    // ==========================================================================

    /// <summary>
    ///     Records a span ingestion with all relevant metrics.
    /// </summary>
    public static void RecordIngestion(
        int spanCount,
        int genAiSpanCount,
        long totalTokens,
        double durationSeconds,
        string? provider = null,
        string? model = null)
    {
        var tags = new TagList();
        if (provider is not null) tags.Add("gen_ai.provider.name", provider);
        if (model is not null) tags.Add("gen_ai.request.model", model);

        SpansIngested.Add(spanCount, tags);

        if (genAiSpanCount > 0)
        {
            GenAiSpansIngested.Add(genAiSpanCount, tags);
        }

        if (totalTokens > 0)
        {
            TokensProcessed.Add(totalTokens, tags);
        }

        IngestionDuration.Record(durationSeconds, tags);
        BatchSize.Record(spanCount, tags);
    }

    /// <summary>
    ///     Records an ingestion error.
    /// </summary>
    public static void RecordError(string errorType, string? provider = null)
    {
        var tags = new TagList { { "error.type", errorType } };
        if (provider is not null) tags.Add("gen_ai.provider.name", provider);

        IngestionErrors.Add(1, tags);
    }

    private static long GetStorageSizeBytes() =>
        s_storageSizeCallback?.Invoke() ?? 0;
}
