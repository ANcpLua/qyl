namespace Qyl.Collector.Storage;

internal sealed class MetricHistogramBucketsJson
{
    public required double[] ExplicitBounds { get; init; }
    public required ulong[] BucketCounts { get; init; }
}

internal sealed class MetricExponentialHistogramBucketsJson
{
    public required int PositiveOffset { get; init; }
    public required ulong[] PositiveBucketCounts { get; init; }
    public required int NegativeOffset { get; init; }
    public required ulong[] NegativeBucketCounts { get; init; }
}

internal sealed class MetricExemplarJson
{
    public required ulong TimeUnixNano { get; init; }
    public long? IntValue { get; init; }
    public double? DoubleValue { get; init; }
    public string? SpanId { get; init; }
    public string? TraceId { get; init; }
    public string? FilteredAttributesJson { get; init; }
}

internal sealed class MetricSummaryQuantileJson
{
    public required double Quantile { get; init; }
    public required double Value { get; init; }
}
