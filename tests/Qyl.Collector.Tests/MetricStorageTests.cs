using OpenTelemetry.Proto.Collector.Metrics.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Metrics.V1;
using OpenTelemetry.Proto.Resource.V1;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Storage;
using OtlpMetric = OpenTelemetry.Proto.Metrics.V1.Metric;

namespace Qyl.Collector.Tests;

public sealed class MetricStorageTests
{
    private const ulong T0 = 1_700_000_000_000_000_000UL;
    private const ulong OneSecond = 1_000_000_000UL;

    [Fact]
    public void Gauge_and_sum_points_convert_with_their_temporality_and_monotonicity()
    {
        var batch = OtlpConverter.ConvertMetrics(Request(
            Gauge("proc.threads", (T0, 4d), (T0 + OneSecond, 6d)),
            new OtlpMetric
            {
                Name = "http.server.requests",
                Unit = "{request}",
                Sum = new Sum
                {
                    IsMonotonic = true,
                    AggregationTemporality = AggregationTemporality.Cumulative,
                    DataPoints = { new NumberDataPoint { TimeUnixNano = T0, AsInt = 42 } }
                }
            }));

        Assert.Equal(0, batch.RejectedDataPoints);
        var gauge = batch.Points.Where(static point => point.MetricName is "proc.threads").ToArray();
        Assert.Equal(2, gauge.Length);
        Assert.All(gauge, static point => Assert.Equal(MetricKind.Gauge, point.Kind));
        Assert.All(gauge, static point => Assert.Equal(MetricTemporality.Unspecified, point.Temporality));
        Assert.Equal([4d, 6d], gauge.Select(static point => point.Value));

        var sum = Assert.Single(batch.Points.Where(static point => point.MetricName is "http.server.requests"));
        Assert.Equal(MetricKind.Sum, sum.Kind);
        Assert.Equal(MetricTemporality.Cumulative, sum.Temporality);
        Assert.True(sum.IsMonotonic);
        // An int-valued point is stored as the same double every aggregate reads.
        Assert.Equal(42d, sum.Value);
        Assert.Equal("{request}", sum.Unit);
    }

    [Fact]
    public void A_no_recorded_value_point_stores_no_value_rather_than_a_meaningless_one()
    {
        var batch = OtlpConverter.ConvertMetrics(Request(new OtlpMetric
        {
            Name = "gap.gauge",
            Gauge = new Gauge
            {
                DataPoints =
                {
                    new NumberDataPoint { TimeUnixNano = T0, AsDouble = 99, Flags = 1 }
                }
            }
        }));

        Assert.Null(Assert.Single(batch.Points).Value);
    }

    [Fact]
    public void Histogram_points_keep_their_explicit_bounds_and_aggregates()
    {
        var batch = OtlpConverter.ConvertMetrics(Request(Histogram(
            "http.server.duration",
            T0,
            count: 4,
            sum: 10,
            bounds: [1.0, 5.0],
            counts: [1, 2, 1])));

        var point = Assert.Single(batch.Points);
        Assert.Equal(MetricKind.Histogram, point.Kind);
        Assert.Equal(4UL, point.Count);
        Assert.Equal(10d, point.Sum);
        Assert.Equal([1.0, 5.0], point.BucketBounds!);
        Assert.Equal([1UL, 2UL, 1UL], point.BucketCounts!);
    }

    [Fact]
    public void An_exponential_histogram_materializes_into_ascending_explicit_buckets()
    {
        // Scale 0 => base 2. Positive offset 1 covers (2, 4], the next (4, 8]; the negative
        // side's single bucket at offset 0 covers [-2, -1), and the zero bucket sits between.
        var batch = OtlpConverter.ConvertMetrics(Request(new OtlpMetric
        {
            Name = "latency.exp",
            ExponentialHistogram = new ExponentialHistogram
            {
                AggregationTemporality = AggregationTemporality.Delta,
                DataPoints =
                {
                    new ExponentialHistogramDataPoint
                    {
                        TimeUnixNano = T0,
                        Scale = 0,
                        Count = 6,
                        Sum = 20,
                        ZeroCount = 1,
                        ZeroThreshold = 0.5,
                        Negative = new ExponentialHistogramDataPoint.Types.Buckets
                        {
                            Offset = 0, BucketCounts = { 2 }
                        },
                        Positive = new ExponentialHistogramDataPoint.Types.Buckets
                        {
                            Offset = 1, BucketCounts = { 1, 2 }
                        }
                    }
                }
            }
        }));

        var point = Assert.Single(batch.Points);
        Assert.Equal(MetricKind.ExponentialHistogram, point.Kind);
        var bounds = point.BucketBounds!;
        Assert.Equal(4, bounds.Count);
        Assert.Equal(-1d, bounds[0], 9);
        Assert.Equal(0.5d, bounds[1], 9);
        Assert.Equal(4d, bounds[2], 9);
        Assert.Equal(8d, bounds[3], 9);
        // Counts are always one longer: the trailing element is the implicit +infinity bucket.
        Assert.Equal([2UL, 1UL, 1UL, 2UL, 0UL], point.BucketCounts!);
    }

    [Fact]
    public void A_summary_is_rejected_by_name_without_dropping_the_metrics_beside_it()
    {
        var batch = OtlpConverter.ConvertMetrics(Request(
            Gauge("kept.gauge", (T0, 1d)),
            new OtlpMetric
            {
                Name = "legacy.summary",
                Summary = new Summary
                {
                    DataPoints = { new SummaryDataPoint(), new SummaryDataPoint() }
                }
            }));

        Assert.Equal(2, batch.RejectedDataPoints);
        Assert.Contains("legacy.summary", batch.RejectionMessage!, StringComparison.Ordinal);
        Assert.Equal("kept.gauge", Assert.Single(batch.Points).MetricName);
    }

    [Fact]
    public void An_unregistered_metric_attribute_is_dropped_by_the_same_policy_spans_use()
    {
        var batch = OtlpConverter.ConvertMetrics(Request(new OtlpMetric
        {
            Name = "policy.gauge",
            Gauge = new Gauge
            {
                DataPoints =
                {
                    new NumberDataPoint
                    {
                        TimeUnixNano = T0,
                        AsDouble = 1,
                        Attributes =
                        {
                            Attr("http.request.method", "GET"),
                            Attr("totally.unregistered", "x"),
                            Attr("user.email", "a@b.c")
                        }
                    }
                }
            }
        }));

        var attributes = Assert.Single(batch.Points).Attributes;
        Assert.Equal(["http.request.method"], attributes.Keys);
    }

    [Fact]
    public async Task Metrics_round_trip_through_storage_and_answer_the_catalog_and_series_questions()
    {
        await using var store = new DuckDbStore(":memory:");
        var ct = TestContext.Current.CancellationToken;

        await IngestAsync(store, ct, Request(
            GaugeWithRoute("http.server.active", "/a", (T0, 2d)),
            GaugeWithRoute("http.server.active", "/b", (T0, 8d))));

        var catalog = await store.ListMetricsAsync(ProjectScope.DefaultProjectId, ct: ct);
        var entry = Assert.Single(catalog);
        Assert.Equal("http.server.active", entry.MetricName);
        Assert.Equal((byte)MetricKind.Gauge, entry.MetricKind);
        // Two attribute sets under one name are two series, not two metrics.
        Assert.Equal(2, entry.SeriesCount);

        var all = await store.ListMetricSeriesAsync(
            ProjectScope.DefaultProjectId, "http.server.active", [], ct: ct);
        Assert.Equal(2, all.Count);

        var exact = await store.ListMetricSeriesAsync(
            ProjectScope.DefaultProjectId,
            "http.server.active",
            [new MetricAttributeMatcher("http.route", "/a", IsPrefix: false)],
            ct: ct);
        Assert.Single(exact);

        var prefixed = await store.ListMetricSeriesAsync(
            ProjectScope.DefaultProjectId,
            "http.server.active",
            [new MetricAttributeMatcher("http.route", "/", IsPrefix: true)],
            ct: ct);
        Assert.Equal(2, prefixed.Count);
    }

    [Fact]
    public async Task Re_ingesting_the_same_stream_updates_one_series_instead_of_forking_it()
    {
        await using var store = new DuckDbStore(":memory:");
        var ct = TestContext.Current.CancellationToken;

        await IngestAsync(store, ct, Request(Gauge("stable.gauge", (T0, 1d))));
        await IngestAsync(store, ct, Request(Gauge("stable.gauge", (T0 + OneSecond, 2d))));

        var series = Assert.Single(await store.ListMetricSeriesAsync(
            ProjectScope.DefaultProjectId, "stable.gauge", [], ct: ct));
        Assert.Equal(T0, series.FirstSeenUnixNano);
        Assert.Equal(T0 + OneSecond, series.LastSeenUnixNano);
    }

    [Theory]
    [InlineData(nameof(MetricAggregation.Avg), 20d)]
    [InlineData(nameof(MetricAggregation.Min), 10d)]
    [InlineData(nameof(MetricAggregation.Max), 30d)]
    [InlineData(nameof(MetricAggregation.Sum), 60d)]
    [InlineData(nameof(MetricAggregation.Count), 3d)]
    [InlineData(nameof(MetricAggregation.Last), 30d)]
    public async Task A_range_query_aggregates_server_side_into_one_bucket(
        string aggregationName,
        double expected)
    {
        var aggregation = Enum.Parse<MetricAggregation>(aggregationName);
        await using var store = new DuckDbStore(":memory:");
        var ct = TestContext.Current.CancellationToken;

        await IngestAsync(store, ct, Request(Gauge(
            "queue.depth",
            (T0, 10d),
            (T0 + OneSecond, 20d),
            (T0 + 2 * OneSecond, 30d))));

        var result = await store.QueryMetricAsync(new MetricRangeQuery
        {
            ProjectId = ProjectScope.DefaultProjectId,
            MetricName = "queue.depth",
            StartUnixNano = T0,
            EndUnixNano = T0 + 10 * OneSecond,
            StepUnixNano = 10 * OneSecond,
            Aggregation = aggregation
        }, ct);

        var point = Assert.Single(Assert.Single(result).Points);
        Assert.Equal(T0, point.BucketStartUnixNano);
        Assert.Equal(expected, point.Value!.Value, 9);
    }

    [Fact]
    public async Task A_range_query_buckets_by_step_and_splits_by_the_requested_attribute()
    {
        await using var store = new DuckDbStore(":memory:");
        var ct = TestContext.Current.CancellationToken;

        await IngestAsync(store, ct, Request(
            GaugeWithRoute("http.server.active", "/a", (T0, 1d), (T0 + 5 * OneSecond, 3d)),
            GaugeWithRoute("http.server.active", "/b", (T0, 10d))));

        var result = await store.QueryMetricAsync(new MetricRangeQuery
        {
            ProjectId = ProjectScope.DefaultProjectId,
            MetricName = "http.server.active",
            StartUnixNano = T0,
            EndUnixNano = T0 + 10 * OneSecond,
            StepUnixNano = 5 * OneSecond,
            Aggregation = MetricAggregation.Avg,
            GroupBy = ["http.route"]
        }, ct);

        Assert.Equal(2, result.Count);
        var routeA = result.Single(static stream => stream.AttributesJson!.Contains("/a", StringComparison.Ordinal));
        Assert.Equal(2, routeA.Points.Count);
        Assert.Equal(T0, routeA.Points[0].BucketStartUnixNano);
        Assert.Equal(1d, routeA.Points[0].Value!.Value, 9);
        Assert.Equal(T0 + 5 * OneSecond, routeA.Points[1].BucketStartUnixNano);
        Assert.Equal(3d, routeA.Points[1].Value!.Value, 9);

        var routeB = result.Single(static stream => stream.AttributesJson!.Contains("/b", StringComparison.Ordinal));
        Assert.Equal(10d, Assert.Single(routeB.Points).Value!.Value, 9);
    }

    [Fact]
    public async Task A_matcher_narrows_a_range_query_to_the_series_it_selects()
    {
        await using var store = new DuckDbStore(":memory:");
        var ct = TestContext.Current.CancellationToken;

        await IngestAsync(store, ct, Request(
            GaugeWithRoute("http.server.active", "/a", (T0, 4d)),
            GaugeWithRoute("http.server.active", "/b", (T0, 100d))));

        var result = await store.QueryMetricAsync(new MetricRangeQuery
        {
            ProjectId = ProjectScope.DefaultProjectId,
            MetricName = "http.server.active",
            StartUnixNano = T0,
            EndUnixNano = T0 + OneSecond,
            StepUnixNano = OneSecond,
            Aggregation = MetricAggregation.Sum,
            Matchers = [new MetricAttributeMatcher("http.route", "/a", IsPrefix: false)]
        }, ct);

        Assert.Equal(4d, Assert.Single(Assert.Single(result).Points).Value!.Value, 9);
    }

    [Fact]
    public async Task A_histogram_query_reads_the_recorded_aggregates_and_interpolates_percentiles()
    {
        await using var store = new DuckDbStore(":memory:");
        var ct = TestContext.Current.CancellationToken;

        // Two points in one step, buckets (-inf,1] (1,5] (5,inf): counts 1,2,1 and 1,2,1
        // fold to 2,4,2, so the median falls in (1,5] at exactly its midpoint.
        await IngestAsync(store, ct, Request(Histogram(
            "http.server.duration", T0, count: 4, sum: 10, bounds: [1.0, 5.0], counts: [1, 2, 1])));
        await IngestAsync(store, ct, Request(Histogram(
            "http.server.duration", T0 + OneSecond, count: 4, sum: 30, bounds: [1.0, 5.0], counts: [1, 2, 1])));

        var request = new MetricRangeQuery
        {
            ProjectId = ProjectScope.DefaultProjectId,
            MetricName = "http.server.duration",
            StartUnixNano = T0,
            EndUnixNano = T0 + 10 * OneSecond,
            StepUnixNano = 10 * OneSecond,
            Aggregation = MetricAggregation.Avg
        };

        // A histogram average is the recorded sum over the recorded count, not an average
        // of per-point averages: (10 + 30) / (4 + 4).
        var average = Assert.Single(Assert.Single(await store.QueryMetricAsync(request, ct)).Points);
        Assert.Equal(5d, average.Value!.Value, 9);

        var count = Assert.Single(Assert.Single(await store.QueryMetricAsync(
            request with { Aggregation = MetricAggregation.Count }, ct)).Points);
        Assert.Equal(8d, count.Value!.Value, 9);

        var median = Assert.Single(Assert.Single(await store.QueryMetricAsync(
            request with { Aggregation = MetricAggregation.P50 }, ct)).Points);
        Assert.Equal(3d, median.Value!.Value, 9);

        var p99 = Assert.Single(Assert.Single(await store.QueryMetricAsync(
            request with { Aggregation = MetricAggregation.P99 }, ct)).Points);
        // The top bucket has no finite upper edge, so the last finite bound is reported.
        Assert.Equal(5d, p99.Value!.Value, 9);
    }

    [Fact]
    public async Task Retention_deletes_expired_points_and_the_series_they_leave_empty()
    {
        await using var store = new DuckDbStore(":memory:");
        var ct = TestContext.Current.CancellationToken;

        await IngestAsync(store, ct, Request(
            Gauge("old.gauge", (T0, 1d)),
            Gauge("new.gauge", (T0 + 100 * OneSecond, 2d))));

        var deleted = await store.DeleteExpiredMetricsBatchAsync(T0 + OneSecond, 100, ct);
        Assert.Equal(1, deleted.Points);
        Assert.Equal(1, deleted.Series);

        var remaining = await store.ListMetricsAsync(ProjectScope.DefaultProjectId, ct: ct);
        Assert.Equal("new.gauge", Assert.Single(remaining).MetricName);
    }

    private static async Task IngestAsync(
        DuckDbStore store,
        CancellationToken ct,
        ExportMetricsServiceRequest request)
    {
        var write = MetricStorageMapper.ToStorageRows(OtlpConverter.ConvertMetrics(request));
        await store.InsertMetricsAsync(write.Series, write.Points, ct);
    }

    private static ExportMetricsServiceRequest Request(params OtlpMetric[] metrics)
    {
        var scope = new ScopeMetrics();
        scope.Metrics.AddRange(metrics);
        return new ExportMetricsServiceRequest
        {
            ResourceMetrics =
            {
                new ResourceMetrics
                {
                    Resource = new Resource
                    {
                        Attributes = { Attr("service.name", "metrics-tests") }
                    },
                    ScopeMetrics = { scope }
                }
            }
        };
    }

    private static OtlpMetric Gauge(string name, params (ulong Time, double Value)[] points)
    {
        var gauge = new Gauge();
        foreach (var (time, value) in points)
            gauge.DataPoints.Add(new NumberDataPoint { TimeUnixNano = time, AsDouble = value });
        return new OtlpMetric { Name = name, Gauge = gauge };
    }

    private static OtlpMetric GaugeWithRoute(
        string name,
        string route,
        params (ulong Time, double Value)[] points)
    {
        var gauge = new Gauge();
        foreach (var (time, value) in points)
        {
            gauge.DataPoints.Add(new NumberDataPoint
            {
                TimeUnixNano = time,
                AsDouble = value,
                Attributes = { Attr("http.route", route) }
            });
        }

        return new OtlpMetric { Name = name, Gauge = gauge };
    }

    private static OtlpMetric Histogram(
        string name,
        ulong time,
        ulong count,
        double sum,
        double[] bounds,
        ulong[] counts)
    {
        var point = new HistogramDataPoint { TimeUnixNano = time, Count = count, Sum = sum };
        point.ExplicitBounds.AddRange(bounds);
        point.BucketCounts.AddRange(counts);
        return new OtlpMetric
        {
            Name = name,
            Histogram = new Histogram
            {
                AggregationTemporality = AggregationTemporality.Delta,
                DataPoints = { point }
            }
        };
    }

    private static KeyValue Attr(string key, string value) =>
        new() { Key = key, Value = new AnyValue { StringValue = value } };
}
