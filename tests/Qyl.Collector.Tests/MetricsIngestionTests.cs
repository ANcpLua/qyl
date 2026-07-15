using OpenTelemetry.Proto.Collector.Metrics.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Metrics.V1;
using OpenTelemetry.Proto.Resource.V1;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests;

public sealed class MetricsIngestionTests
{
    [Fact]
    public async Task Sum_and_gauge_data_points_round_trip_through_the_store()
    {
        await using var store = new DuckDbStore(":memory:");

        var request = RequestWithResource(resource =>
        {
            resource.ScopeMetrics.Add(new ScopeMetrics
            {
                Scope = new InstrumentationScope { Name = "Experimental.Microsoft.Extensions.AI" },
                Metrics =
                {
                    new Metric
                    {
                        Name = "gen_ai.client.token.usage",
                        Unit = "{token}",
                        Sum = new Sum
                        {
                            IsMonotonic = true,
                            AggregationTemporality = AggregationTemporality.Cumulative,
                            DataPoints =
                            {
                                NumberPoint(timeNano: 100, asInt: 12_574,
                                    ("gen_ai.token.type", "input"),
                                    ("gen_ai.request.model", "claude-sonnet-5")),
                                NumberPoint(timeNano: 100, asInt: 512,
                                    ("gen_ai.token.type", "output"),
                                    ("gen_ai.request.model", "claude-sonnet-5"))
                            }
                        }
                    },
                    new Metric
                    {
                        Name = "process.runtime.gc.heap.size",
                        Unit = "By",
                        Gauge = new Gauge { DataPoints = { NumberPoint(timeNano: 200, asDouble: 1024.5) } }
                    }
                }
            });
        });

        var rows = IngestionStorageMapper.ToMetricStorageRows(OtlpConverter.ConvertMetrics(request));
        await store.InsertMetricsAsync(rows, TestContext.Current.CancellationToken);

        var stored = await store.GetMetricsAsync("default", ct: TestContext.Current.CancellationToken);
        Assert.Equal(3, stored.Count);

        var tokenPoints = stored.Where(m => m.MetricName == "gen_ai.client.token.usage").ToArray();
        Assert.Equal(2, tokenPoints.Length);
        Assert.All(tokenPoints, point =>
        {
            Assert.Equal(2, point.MetricType);
            Assert.Equal("{token}", point.Unit);
            Assert.Equal((byte)1, point.IsMonotonic);
            Assert.Equal("Experimental.Microsoft.Extensions.AI", point.ScopeName);
            Assert.Equal("test-service", point.ServiceName);
            Assert.NotNull(point.AttributesJson);
            Assert.Contains("gen_ai.token.type", point.AttributesJson);
        });
        Assert.Equal([512d, 12_574d], tokenPoints.Select(p => p.Value!.Value).Order());

        var gauge = Assert.Single(stored, m => m.MetricName == "process.runtime.gc.heap.size");
        Assert.Equal(1, gauge.MetricType);
        Assert.Equal(1024.5, gauge.Value);
    }

    [Fact]
    public async Task Histogram_points_persist_count_sum_bounds_and_bucket_counts()
    {
        await using var store = new DuckDbStore(":memory:");

        var request = RequestWithResource(resource =>
        {
            resource.ScopeMetrics.Add(new ScopeMetrics
            {
                Metrics =
                {
                    new Metric
                    {
                        Name = "http.server.request.duration",
                        Unit = "s",
                        Histogram = new Histogram
                        {
                            AggregationTemporality = AggregationTemporality.Cumulative,
                            DataPoints =
                            {
                                new HistogramDataPoint
                                {
                                    TimeUnixNano = 300,
                                    StartTimeUnixNano = 1,
                                    Count = 4,
                                    Sum = 0.42,
                                    Min = 0.01,
                                    Max = 0.3,
                                    ExplicitBounds = { 0.05, 0.1, 0.5 },
                                    BucketCounts = { 1, 1, 2, 0 },
                                    Attributes =
                                    {
                                        new KeyValue
                                        {
                                            Key = "http.request.method",
                                            Value = new AnyValue { StringValue = "POST" }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            });
        });

        var rows = IngestionStorageMapper.ToMetricStorageRows(OtlpConverter.ConvertMetrics(request));
        await store.InsertMetricsAsync(rows, TestContext.Current.CancellationToken);

        var stored = await store.GetMetricsAsync(
            "default", metricName: "http.server.request.duration", ct: TestContext.Current.CancellationToken);
        var histogram = Assert.Single(stored);
        Assert.Equal(3, histogram.MetricType);
        Assert.Equal(4ul, histogram.Count);
        Assert.Equal(0.42, histogram.Sum);
        Assert.Equal(0.01, histogram.Min);
        Assert.Equal(0.3, histogram.Max);
        Assert.NotNull(histogram.BucketsJson);
        Assert.Contains("\"bounds\":[0.05,0.1,0.5]", histogram.BucketsJson);
        Assert.Contains("\"counts\":[1,1,2,0]", histogram.BucketsJson);
        Assert.Contains("http.request.method", histogram.AttributesJson);
    }

    [Fact]
    public async Task Re_exported_data_points_upsert_instead_of_duplicating()
    {
        await using var store = new DuckDbStore(":memory:");

        var request = RequestWithResource(resource => resource.ScopeMetrics.Add(new ScopeMetrics
        {
            Metrics =
            {
                new Metric
                {
                    Name = "http.server.active_requests",
                    Sum = new Sum { DataPoints = { NumberPoint(timeNano: 400, asInt: 3) } }
                }
            }
        }));

        var rows = IngestionStorageMapper.ToMetricStorageRows(OtlpConverter.ConvertMetrics(request));
        await store.InsertMetricsAsync(rows, TestContext.Current.CancellationToken);
        await store.InsertMetricsAsync(rows, TestContext.Current.CancellationToken);

        var stored = await store.GetMetricsAsync("default", ct: TestContext.Current.CancellationToken);
        Assert.Single(stored);
    }

    private static ExportMetricsServiceRequest RequestWithResource(Action<ResourceMetrics> configure)
    {
        var resourceMetrics = new ResourceMetrics
        {
            Resource = new Resource
            {
                Attributes =
                {
                    new KeyValue
                    {
                        Key = "service.name",
                        Value = new AnyValue { StringValue = "test-service" }
                    }
                }
            }
        };
        configure(resourceMetrics);
        return new ExportMetricsServiceRequest { ResourceMetrics = { resourceMetrics } };
    }

    private static NumberDataPoint NumberPoint(
        ulong timeNano,
        long? asInt = null,
        params (string Key, string Value)[] attributes) =>
        NumberPointCore(timeNano, asInt, null, attributes);

    private static NumberDataPoint NumberPoint(
        ulong timeNano,
        double asDouble,
        params (string Key, string Value)[] attributes) =>
        NumberPointCore(timeNano, null, asDouble, attributes);

    private static NumberDataPoint NumberPointCore(
        ulong timeNano,
        long? asInt,
        double? asDouble,
        (string Key, string Value)[] attributes)
    {
        var point = new NumberDataPoint { TimeUnixNano = timeNano };
        if (asInt is { } intValue) point.AsInt = intValue;
        if (asDouble is { } doubleValue) point.AsDouble = doubleValue;
        foreach (var (key, value) in attributes)
        {
            point.Attributes.Add(new KeyValue
            {
                Key = key,
                Value = new AnyValue { StringValue = value }
            });
        }

        return point;
    }
}
