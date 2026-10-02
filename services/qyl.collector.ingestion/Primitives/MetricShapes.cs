namespace Qyl.Collector.Primitives;

/// <summary>
/// The metric shapes qyl persists. OTLP's summary point is deliberately absent: it carries
/// pre-computed quantiles with no buckets, so it can be neither re-aggregated over a time
/// window nor merged across series, and no OpenTelemetry SDK emits it. Ingest rejects it
/// explicitly rather than storing a shape the read API could not answer a question about.
/// </summary>
internal enum MetricKind : byte
{
    Gauge = 1,
    Sum = 2,
    Histogram = 3,

    /// <summary>
    /// An OTLP exponential histogram. Its buckets are materialized into the same explicit
    /// lower/upper bound vector every <see cref="Histogram" /> point uses, so the query path
    /// has exactly one histogram shape; the kind is retained only to report provenance.
    /// </summary>
    ExponentialHistogram = 4
}

internal enum MetricTemporality : byte
{
    Unspecified = 0,
    Delta = 1,
    Cumulative = 2
}
