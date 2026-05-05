

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Otel;

public static class OtelAttributes
{
    public const string ComponentName = "otel.component.name";

    public const string ComponentType = "otel.component.type";

    public static class ComponentTypeValues
    {
        public const string BatchingLogProcessor = "batching_log_processor";

        public const string BatchingSpanProcessor = "batching_span_processor";

        public const string OtlpGrpcLogExporter = "otlp_grpc_log_exporter";

        public const string OtlpGrpcMetricExporter = "otlp_grpc_metric_exporter";

        public const string OtlpGrpcSpanExporter = "otlp_grpc_span_exporter";

        public const string OtlpHttpJsonLogExporter = "otlp_http_json_log_exporter";

        public const string OtlpHttpJsonMetricExporter = "otlp_http_json_metric_exporter";

        public const string OtlpHttpJsonSpanExporter = "otlp_http_json_span_exporter";

        public const string OtlpHttpLogExporter = "otlp_http_log_exporter";

        public const string OtlpHttpMetricExporter = "otlp_http_metric_exporter";

        public const string OtlpHttpSpanExporter = "otlp_http_span_exporter";

        public const string PeriodicMetricReader = "periodic_metric_reader";

        public const string PrometheusHttpTextMetricExporter = "prometheus_http_text_metric_exporter";

        public const string SimpleLogProcessor = "simple_log_processor";

        public const string SimpleSpanProcessor = "simple_span_processor";

        public const string ZipkinHttpSpanExporter = "zipkin_http_span_exporter";
    }

    [global::System.Obsolete("Replaced by otel.scope.name.", false)]
    public const string LibraryName = "otel.library.name";

    [global::System.Obsolete("Replaced by otel.scope.version.", false)]
    public const string LibraryVersion = "otel.library.version";

    public const string ScopeSchemaUrl = "otel.scope.schema_url";

    public const string SpanParentOrigin = "otel.span.parent.origin";

    public static class SpanParentOriginValues
    {
        public const string Local = "local";

        public const string None = "none";

        public const string Remote = "remote";
    }

    public const string SpanSamplingResult = "otel.span.sampling_result";

    public static class SpanSamplingResultValues
    {
        public const string Drop = "DROP";

        public const string RecordAndSample = "RECORD_AND_SAMPLE";

        public const string RecordOnly = "RECORD_ONLY";
    }
}
