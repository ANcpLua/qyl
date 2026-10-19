using Qyl.Api.Contracts.Common;
using Qyl.Api.Contracts.OTel.Metrics;
using ContractAttribute = Qyl.Api.Contracts.Common.Attribute;

namespace Qyl.Collector.Storage;

/// <summary>
/// The ingestion boundary. Decoding hands storage OTLP attribute values and generated contract metric
/// data; both become storage's own records here, before anything is encoded or hashed, so neither a
/// contract serializer nor a contract change can reach persisted bytes or identifiers.
/// </summary>
internal static class StoredRecordMapper
{
    internal static StoredAttributeValue? ToStored(OtlpAttributeValue value) => ToStored(value.ToContract());

    internal static StoredAttributeValue? ToStored(AttributeValue? value) => value switch
    {
        null => null,
        AttributeValue.StringValue text => new StoredAttributeValue.StringValue(text.Value),
        AttributeValue.BoolValue flag => new StoredAttributeValue.BoolValue(flag.Value),
        AttributeValue.ArrayValue array => new StoredAttributeValue.ArrayValue([.. array.Value.Select(static item => ToStored(item))]),
        AttributeValue.ObjectValue tagged => new StoredAttributeValue.ObjectValue(ToStoredObject(tagged.Value)),
        _ => throw new InvalidDataException("Unknown OTLP attribute value shape.")
    };

    internal static StoredMetricPointData ToStored(MetricPointData data) => data switch
    {
        MetricNumberPointData number => new StoredNumberPointData
        {
            Value = number.Value is { } value ? ToStoredNumber(value) : null
        },
        MetricHistogramPointData histogram => new StoredHistogramPointData
        {
            Count = histogram.Count, Sum = histogram.Sum, Min = histogram.Min, Max = histogram.Max,
            ExplicitBounds = histogram.ExplicitBounds, BucketCounts = histogram.BucketCounts
        },
        MetricExponentialHistogramPointData exponential => new StoredExponentialHistogramPointData
        {
            Count = exponential.Count, Sum = exponential.Sum, Min = exponential.Min, Max = exponential.Max,
            Scale = exponential.Scale, ZeroCount = exponential.ZeroCount, ZeroThreshold = exponential.ZeroThreshold,
            Positive = ToStored(exponential.Positive), Negative = ToStored(exponential.Negative)
        },
        MetricSummaryPointData summary => new StoredSummaryPointData
        {
            Count = summary.Count, Sum = summary.Sum,
            QuantileValues = [.. summary.QuantileValues.Select(static quantile => new StoredQuantileValue
            {
                Quantile = quantile.Quantile, Value = quantile.Value
            })]
        },
        _ => throw new InvalidDataException("Unknown OTLP metric data shape.")
    };

    /// <summary>Exemplars keep producer order; it is part of the observation.</summary>
    internal static IReadOnlyList<StoredExemplar> ToStored(IReadOnlyList<MetricExemplar> exemplars) =>
    [
        .. exemplars.Select(static exemplar => new StoredExemplar
        {
            TimeUnixNano = exemplar.TimeUnixNano, Value = ToStoredNumber(exemplar.Value),
            FilteredAttributes = ToStored(exemplar.FilteredAttributes),
            TraceId = exemplar.TraceId, SpanId = exemplar.SpanId
        })
    ];

    internal static IReadOnlyList<StoredAttribute> ToStored(IReadOnlyList<ContractAttribute> attributes) =>
    [
        .. attributes.OrderBy(static attribute => attribute.Key, StringComparer.Ordinal)
            .Select(static attribute => new StoredAttribute { Key = attribute.Key, Value = ToStored(attribute.Value) })
    ];

    private static StoredObjectValue ToStoredObject(AttributeObjectValue value) => value switch
    {
        AttributeIntValue integer => new StoredIntValue { Value = integer.Value },
        AttributeDoubleValue number => new StoredDoubleValue { Value = number.Value },
        AttributeBytesValue bytes => new StoredBytesValue { Base64 = bytes.Base64 },
        // Persisted maps keep ordinal key order, whatever order the producer used.
        AttributeKeyValueListValue map => new StoredKeyValueListValue
        {
            Values = map.Values.OrderBy(static item => item.Key, StringComparer.Ordinal)
                .ToDictionary(static item => item.Key, static item => ToStored(item.Value), StringComparer.Ordinal)
        },
        _ => throw new InvalidDataException("Unknown OTLP attribute object shape.")
    };

    private static StoredNumberValue ToStoredNumber(MetricNumberValue value) => value switch
    {
        AttributeIntValue integer => new StoredIntValue { Value = integer.Value },
        AttributeDoubleValue number => new StoredDoubleValue { Value = number.Value },
        _ => throw new InvalidDataException("Unknown OTLP metric numeric representation.")
    };

    private static StoredExponentialBuckets? ToStored(MetricExponentialBuckets? buckets) => buckets is null
        ? null
        : new StoredExponentialBuckets { Offset = buckets.Offset, BucketCounts = buckets.BucketCounts };
}
