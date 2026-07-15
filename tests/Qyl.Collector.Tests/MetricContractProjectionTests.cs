using System.Text.Json;
using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Metrics.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Metrics.V1;
using OpenTelemetry.Proto.Resource.V1;
using Qyl.Api.Contracts.OTel.Enums;
using Qyl.Api.Contracts.OTel.Metrics;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Mapping;
using Qyl.Collector.Storage;
using ContractAggregationTemporality = Qyl.Api.Contracts.OTel.Enums.AggregationTemporality;
using ProtoAggregationTemporality = OpenTelemetry.Proto.Metrics.V1.AggregationTemporality;
using ProtoInstrumentationScope = OpenTelemetry.Proto.Common.V1.InstrumentationScope;

namespace Qyl.Collector.Tests;

public sealed class MetricContractProjectionTests
{
    [Fact]
    public async Task Every_metric_variant_preserves_the_generated_contract_projection()
    {
        var request = BuildCompleteRequest();
        var rows = IngestionStorageMapper.ToMetricStorageRows(OtlpConverter.ConvertMetrics(request));
        Assert.Equal(5, rows.Count);
        Assert.All(rows, static row =>
        {
            Assert.Equal(MetricStorageRow.CurrentContractProjectionVersion, row.ContractProjectionVersion);
            Assert.NotNull(row.StartTimeUnixNano);
            Assert.NotNull(row.Flags);
            Assert.NotNull(row.ResourceDroppedAttributesCount);
            Assert.NotNull(row.HasInstrumentationScope);
            Assert.NotNull(row.ScopeDroppedAttributesCount);
        });

        await using var store = new DuckDbStore(":memory:");
        await store.InsertMetricsAsync(rows, TestContext.Current.CancellationToken);
        var stored = await store.GetMetricsAsync("default", ct: TestContext.Current.CancellationToken);
        var contracts = stored.ToDictionary(
            static row => row.MetricName,
            static row => MetricMapper.ToContract(row),
            StringComparer.Ordinal);

        var gauge = Assert.IsType<GaugeMetricPoint>(contracts["test.gauge"]);
        Assert.Equal(long.MaxValue, Assert.IsType<MetricIntegerValue>(gauge.Value).AsInt);
        Assert.Equal<ulong>(0, gauge.StartTimeUnixNano);
        Assert.Equal<ulong>(100, gauge.TimeUnixNano);
        Assert.Equal<uint>(1, gauge.Flags);
        Assert.Equal("https://opentelemetry.io/schemas/1.38.0", gauge.ResourceSchemaUrl);
        Assert.Equal(2, gauge.Resource.DroppedAttributesCount);
        Assert.Contains(gauge.Metadata!, static attribute =>
            attribute.Key == "translation.schema" && Equals(attribute.Value, "v1"));
        var scope = Assert.IsType<Qyl.Api.Contracts.Common.InstrumentationScope>(gauge.InstrumentationScope);
        Assert.Equal("test.scope", scope.ScopeName);
        Assert.Equal("1.2.3", scope.ScopeVersion);
        Assert.Equal(3, scope.DroppedAttributesCount);
        Assert.Contains(scope.ScopeAttributes!, static attribute =>
            attribute.Key == "scope.custom" && Equals(attribute.Value, "scope-value"));
        Assert.Equal("https://opentelemetry.io/schemas/1.39.0", gauge.ScopeSchemaUrl);
        var exemplar = Assert.Single(gauge.Exemplars!);
        Assert.Equal(-7, Assert.IsType<MetricIntegerValue>(exemplar.Value).AsInt);
        Assert.Equal("0101010101010101", exemplar.SpanId);
        Assert.Equal("02020202020202020202020202020202", exemplar.TraceId);
        Assert.Contains(exemplar.FilteredAttributes!, static attribute =>
            attribute.Key == "exemplar.custom" && Equals(attribute.Value, "filtered"));

        var sum = Assert.IsType<SumMetricPoint>(contracts["test.sum"]);
        Assert.Equal(1.5, Assert.IsType<MetricDoubleValue>(sum.Value).AsDouble);
        Assert.Equal(ContractAggregationTemporality.Delta, sum.AggregationTemporality);
        Assert.True(sum.IsMonotonic);

        var histogram = Assert.IsType<HistogramMetricPoint>(contracts["test.histogram"]);
        Assert.Equal<ulong>(4, histogram.Count);
        Assert.Equal([1UL, 2UL, 1UL], histogram.BucketCounts);
        Assert.Equal([0.1, 1.0], histogram.ExplicitBounds);
        Assert.Equal(0.05, histogram.Min);
        Assert.Equal(2.0, histogram.Max);
        Assert.Equal(ContractAggregationTemporality.Cumulative, histogram.AggregationTemporality);

        var exponential = Assert.IsType<ExponentialHistogramMetricPoint>(contracts["test.exponential"]);
        Assert.Equal(-2, exponential.Scale);
        Assert.Equal<ulong>(1, exponential.ZeroCount);
        Assert.Equal(0.001, exponential.ZeroThreshold);
        Assert.Equal(4, exponential.Positive.Offset);
        Assert.Equal([2UL, 3UL], exponential.Positive.BucketCounts);
        Assert.Equal(-6, exponential.Negative.Offset);
        Assert.Equal([1UL], exponential.Negative.BucketCounts);

        var summary = Assert.IsType<SummaryMetricPoint>(contracts["test.summary"]);
        Assert.Equal<ulong>(10, summary.Count);
        Assert.Equal(25.0, summary.Sum);
        Assert.Collection(
            summary.QuantileValues,
            value =>
            {
                Assert.Equal(0.5, value.Quantile);
                Assert.Equal(2.0, value.Value);
            },
            value =>
            {
                Assert.Equal(0.9, value.Quantile);
                Assert.Equal(4.0, value.Value);
            });
        Assert.Equal<uint>(1, summary.Flags);
    }

    [Fact]
    public void Metric_identity_is_stable_but_keeps_distinct_stream_semantics_and_attribute_types()
    {
        var baseline = BuildCompleteRequest();
        var baselineRows = RowsByName(baseline);

        var reordered = baseline.Clone();
        var resourceAttributes = reordered.ResourceMetrics[0].Resource.Attributes;
        (resourceAttributes[0], resourceAttributes[1]) = (resourceAttributes[1], resourceAttributes[0]);
        Assert.Equal(baselineRows["test.gauge"].MetricId, RowsByName(reordered)["test.gauge"].MetricId);

        var differentMonotonicity = baseline.Clone();
        differentMonotonicity.ResourceMetrics[0].ScopeMetrics[0].Metrics
            .Single(static metric => metric.Name == "test.sum").Sum.IsMonotonic = false;
        Assert.NotEqual(
            baselineRows["test.sum"].MetricId,
            RowsByName(differentMonotonicity)["test.sum"].MetricId);

        var differentTemporality = baseline.Clone();
        differentTemporality.ResourceMetrics[0].ScopeMetrics[0].Metrics
            .Single(static metric => metric.Name == "test.sum").Sum.AggregationTemporality =
            ProtoAggregationTemporality.Cumulative;
        Assert.NotEqual(
            baselineRows["test.sum"].MetricId,
            RowsByName(differentTemporality)["test.sum"].MetricId);

        var differentGaugeStart = baseline.Clone();
        differentGaugeStart.ResourceMetrics[0].ScopeMetrics[0].Metrics
            .Single(static metric => metric.Name == "test.gauge").Gauge.DataPoints[0].StartTimeUnixNano = 99;
        Assert.Equal(
            baselineRows["test.gauge"].MetricId,
            RowsByName(differentGaugeStart)["test.gauge"].MetricId);

        var integerAttribute = baseline.Clone();
        integerAttribute.ResourceMetrics[0].ScopeMetrics[0].Metrics
            .Single(static metric => metric.Name == "test.gauge").Gauge.DataPoints[0].Attributes.Add(
                new KeyValue { Key = "custom.numeric", Value = new AnyValue { IntValue = 1 } });
        var doubleAttribute = integerAttribute.Clone();
        doubleAttribute.ResourceMetrics[0].ScopeMetrics[0].Metrics
            .Single(static metric => metric.Name == "test.gauge").Gauge.DataPoints[0].Attributes[0].Value =
            new AnyValue { DoubleValue = 1 };
        Assert.NotEqual(
            RowsByName(integerAttribute)["test.gauge"].MetricId,
            RowsByName(doubleAttribute)["test.gauge"].MetricId);
    }

    [Fact]
    public void Named_floating_point_values_survive_storage_and_contract_json()
    {
        var request = BuildCompleteRequest();
        var gaugePoint = request.ResourceMetrics[0].ScopeMetrics[0].Metrics
            .Single(static metric => metric.Name == "test.gauge").Gauge.DataPoints[0];
        gaugePoint.AsDouble = double.NaN;
        gaugePoint.Exemplars[0].AsDouble = double.PositiveInfinity;

        var contract = MetricMapper.ToContract(RowsByName(request)["test.gauge"]);
        var json = JsonSerializer.Serialize(contract, QylSerializerContext.Default.MetricPoint);

        Assert.Contains("\"as_double\":\"NaN\"", json, StringComparison.Ordinal);
        Assert.Contains("\"as_double\":\"Infinity\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Legacy_lossy_metric_rows_are_not_fabricated_as_public_contracts()
    {
        var legacy = new MetricStorageRow
        {
            ProjectId = "default",
            MetricId = "metric_00000000000000000000000000000000",
            MetricName = "legacy.gauge",
            MetricType = MetricStorageTypes.Gauge,
            TimeUnixNano = 1,
            Value = 42,
            ServiceName = "legacy-service"
        };

        Assert.False(MetricMapper.TryToContract(legacy, out _));
    }

    [Fact]
    public void Invalid_points_are_rejected_before_the_lossless_projection_marker_is_written()
    {
        var request = new ExportMetricsServiceRequest
        {
            ResourceMetrics =
            {
                new ResourceMetrics
                {
                    ScopeMetrics =
                    {
                        new ScopeMetrics
                        {
                            Metrics =
                            {
                                new Metric
                                {
                                    Name = "invalid.sum",
                                    Sum = new Sum
                                    {
                                        AggregationTemporality = ProtoAggregationTemporality.Unspecified,
                                        DataPoints = { new NumberDataPoint { TimeUnixNano = 1, AsInt = 1 } }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        Assert.Throws<InvalidDataException>(() => OtlpConverter.ConvertMetrics(request));

        var missingNumber = BuildCompleteRequest();
        missingNumber.ResourceMetrics[0].ScopeMetrics[0].Metrics
            .Single(static metric => metric.Name == "test.gauge").Gauge.DataPoints[0].ClearAsInt();
        Assert.Throws<InvalidDataException>(() => OtlpConverter.ConvertMetrics(missingNumber));
    }

    private static Dictionary<string, MetricStorageRow> RowsByName(ExportMetricsServiceRequest request) =>
        IngestionStorageMapper.ToMetricStorageRows(OtlpConverter.ConvertMetrics(request))
            .ToDictionary(static row => row.MetricName, StringComparer.Ordinal);

    private static ExportMetricsServiceRequest BuildCompleteRequest()
    {
        var scopeMetrics = new ScopeMetrics
        {
            SchemaUrl = "https://opentelemetry.io/schemas/1.39.0",
            Scope = new ProtoInstrumentationScope
            {
                Name = "test.scope",
                Version = "1.2.3",
                DroppedAttributesCount = 3,
                Attributes =
                {
                    StringAttribute("scope.custom", "scope-value")
                }
            }
        };

        var gaugePoint = new NumberDataPoint
        {
            StartTimeUnixNano = 0,
            TimeUnixNano = 100,
            Flags = 1,
            AsInt = long.MaxValue,
            Exemplars =
            {
                new Exemplar
                {
                    TimeUnixNano = 99,
                    AsInt = -7,
                    SpanId = ByteString.CopyFrom(Enumerable.Repeat((byte)1, 8).ToArray()),
                    TraceId = ByteString.CopyFrom(Enumerable.Repeat((byte)2, 16).ToArray()),
                    FilteredAttributes = { StringAttribute("exemplar.custom", "filtered") }
                }
            }
        };
        scopeMetrics.Metrics.Add(new Metric
        {
            Name = "test.gauge",
            Description = "exact integer gauge",
            Unit = "1",
            Metadata = { StringAttribute("translation.schema", "v1") },
            Gauge = new Gauge { DataPoints = { gaugePoint } }
        });

        scopeMetrics.Metrics.Add(new Metric
        {
            Name = "test.sum",
            Sum = new Sum
            {
                AggregationTemporality = ProtoAggregationTemporality.Delta,
                IsMonotonic = true,
                DataPoints =
                {
                    new NumberDataPoint
                    {
                        StartTimeUnixNano = 10,
                        TimeUnixNano = 110,
                        AsDouble = 1.5
                    }
                }
            }
        });

        scopeMetrics.Metrics.Add(new Metric
        {
            Name = "test.histogram",
            Histogram = new Histogram
            {
                AggregationTemporality = ProtoAggregationTemporality.Cumulative,
                DataPoints =
                {
                    new HistogramDataPoint
                    {
                        StartTimeUnixNano = 10,
                        TimeUnixNano = 120,
                        Count = 4,
                        Sum = 3.25,
                        Min = 0.05,
                        Max = 2.0,
                        ExplicitBounds = { 0.1, 1.0 },
                        BucketCounts = { 1, 2, 1 }
                    }
                }
            }
        });

        scopeMetrics.Metrics.Add(new Metric
        {
            Name = "test.exponential",
            ExponentialHistogram = new ExponentialHistogram
            {
                AggregationTemporality = ProtoAggregationTemporality.Delta,
                DataPoints =
                {
                    new ExponentialHistogramDataPoint
                    {
                        StartTimeUnixNano = 10,
                        TimeUnixNano = 130,
                        Count = 7,
                        Sum = 12.5,
                        Scale = -2,
                        ZeroCount = 1,
                        ZeroThreshold = 0.001,
                        Positive = new ExponentialHistogramDataPoint.Types.Buckets
                        {
                            Offset = 4,
                            BucketCounts = { 2, 3 }
                        },
                        Negative = new ExponentialHistogramDataPoint.Types.Buckets
                        {
                            Offset = -6,
                            BucketCounts = { 1 }
                        }
                    }
                }
            }
        });

        scopeMetrics.Metrics.Add(new Metric
        {
            Name = "test.summary",
            Summary = new Summary
            {
                DataPoints =
                {
                    new SummaryDataPoint
                    {
                        StartTimeUnixNano = 0,
                        TimeUnixNano = 140,
                        Flags = 1,
                        Count = 10,
                        Sum = 25,
                        QuantileValues =
                        {
                            new SummaryDataPoint.Types.ValueAtQuantile { Quantile = 0.5, Value = 2 },
                            new SummaryDataPoint.Types.ValueAtQuantile { Quantile = 0.9, Value = 4 }
                        }
                    }
                }
            }
        });

        return new ExportMetricsServiceRequest
        {
            ResourceMetrics =
            {
                new ResourceMetrics
                {
                    SchemaUrl = "https://opentelemetry.io/schemas/1.38.0",
                    Resource = new Resource
                    {
                        DroppedAttributesCount = 2,
                        Attributes =
                        {
                            StringAttribute("service.name", "metric-service"),
                            StringAttribute("service.namespace", "tests")
                        }
                    },
                    ScopeMetrics = { scopeMetrics }
                }
            }
        };
    }

    private static KeyValue StringAttribute(string key, string value) =>
        new() { Key = key, Value = new AnyValue { StringValue = value } };
}
