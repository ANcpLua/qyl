using Qyl.Api.Contracts.Common;
using Qyl.Api.Contracts.OTel.Metrics;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Storage;
using MetricKind = Qyl.Collector.Ingestion.Primitives.MetricKind;
using MetricTemporality = Qyl.Collector.Ingestion.Primitives.MetricTemporality;

namespace Qyl.Collector.Tests;

/// <summary>Canonical inputs for analysis tests whose given values are already explicit scalar or histogram fixtures.</summary>
internal static class MetricFixtures
{
    internal static MetricObservationRow Observation(MetricSeriesRow series, MetricPointRow point,
        IReadOnlyList<MetricExemplar>? exemplars = null)
    {
        MetricPointData data = series.MetricKind switch
        {
            (byte)MetricKind.Gauge or (byte)MetricKind.Sum => new MetricNumberPointData
            {
                Value = point.Value is { } value ? new AttributeDoubleValue { Value = value } : null
            },
            (byte)MetricKind.Histogram => new MetricHistogramPointData
            {
                Count = point.HistogramCount ?? throw new InvalidDataException("Histogram fixtures require a count."),
                Sum = point.HistogramSum, Min = point.HistogramMin, Max = point.HistogramMax,
                ExplicitBounds = System.Text.Json.JsonSerializer.Deserialize(point.BucketBoundsJson ?? "[]", StorageJsonSerializerContext.Default.Doubles) ?? throw new InvalidDataException("Histogram fixture bounds are null."),
                BucketCounts = System.Text.Json.JsonSerializer.Deserialize(point.BucketCountsJson ?? "[]", StorageJsonSerializerContext.Default.Counts) ?? throw new InvalidDataException("Histogram fixture counts are null.")
            },
            _ => throw new InvalidDataException("Use native OTLP fixtures for this metric kind.")
        };
        return MetricObservationCodec.FromIngestion(new MetricPointIngestionRecord
        {
            MetricName = series.MetricName, Kind = (MetricKind)series.MetricKind,
            Temporality = (MetricTemporality)series.Temporality, IsMonotonic = series.IsMonotonic != 0,
            ServiceName = series.ServiceName ?? "test", Attributes = new Dictionary<string, OtlpAttributeValue>(),
            ResourceAttributes = new Dictionary<string, OtlpAttributeValue>(),
            TimeUnixNano = point.TimeUnixNano, StartTimeUnixNano = point.StartTimeUnixNano,
            Data = data, Exemplars = exemplars ?? []
        }, point.ProjectId, point.SeriesId);
    }
}
