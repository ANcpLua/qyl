namespace qyl.Grpc.Models;

public sealed record MetricModel(
    string Name,
    string? Description,
    string? Unit,
    MetricType Type,
    IReadOnlyList<DataPointModel> DataPoints,
    ResourceModel Resource);

public enum MetricType
{
    Gauge = 0,
    Sum = 1,
    Histogram = 2,
    ExponentialHistogram = 3,
    Summary = 4
}

public sealed record DataPointModel(
    DateTimeOffset Timestamp,
    IReadOnlyDictionary<string, AttributeValue> Attributes,
    DataPointValue Value);

public abstract record DataPointValue;

public sealed record GaugeValue(double Value) : DataPointValue;

public sealed record SumValue(double Value, bool IsMonotonic) : DataPointValue;

public sealed record HistogramValue(
    long Count,
    double Sum,
    double? Min,
    double? Max,
    IReadOnlyList<double> ExplicitBounds,
    IReadOnlyList<long> BucketCounts) : DataPointValue;

public sealed record SummaryValue(
    long Count,
    double Sum,
    IReadOnlyList<QuantileValue> Quantiles) : DataPointValue;

public sealed record QuantileValue(double Quantile, double Value);
