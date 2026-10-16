using MetricContract = Qyl.Api.Contracts.OTel.Metrics;
using CommonContract = Qyl.Api.Contracts.Common;
using OpenTelemetry.Proto.Collector.Metrics.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Metrics.V1;
using OpenTelemetry.Proto.Resource.V1;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Ingestion.Primitives;
using Qyl.Collector.Storage;
using OtlpMetric = OpenTelemetry.Proto.Metrics.V1.Metric;

namespace Qyl.Collector.Tests;

[TestClass]
public sealed class MetricStorageTests(TestContext testContext)
{
    private const ulong T0 = 1_700_000_000_000_000_000UL;
    private const ulong OneSecond = 1_000_000_000UL;

    [TestMethod]
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

        Assert.AreEqual(0, batch.RejectedDataPoints);
        var gauge = batch.Points.Where(static point => point.MetricName is "proc.threads").ToArray();
        Assert.AreEqual(2, gauge.Length);
        foreach (var point in gauge) Assert.AreEqual(MetricKind.Gauge, point.Kind);
        foreach (var point in gauge) Assert.AreEqual(MetricTemporality.Unspecified, point.Temporality);
        Assert.AreSequenceEqual([4d, 6d], gauge.Select(static point => Assert.IsExactInstanceOfType<CommonContract.AttributeDoubleValue>(Assert.IsExactInstanceOfType<MetricContract.MetricNumberPointData>(point.Data).Value).Value));

        var sum = Assert.ContainsSingle(static point => point.MetricName is "http.server.requests",batch.Points);
        Assert.AreEqual(MetricKind.Sum, sum.Kind);
        Assert.AreEqual(MetricTemporality.Cumulative, sum.Temporality);
        Assert.IsTrue(sum.IsMonotonic);
        // Canonical storage retains the original integer representation.
        Assert.AreEqual(42L, Assert.IsExactInstanceOfType<CommonContract.AttributeIntValue>(
            Assert.IsExactInstanceOfType<MetricContract.MetricNumberPointData>(sum.Data).Value).Value);
        Assert.AreEqual("{request}", sum.Unit);
    }

    [TestMethod]
    public void A_no_recorded_value_point_keeps_raw_data_but_excludes_it_from_the_projection()
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

        var point = Assert.ContainsSingle(batch.Points);
        Assert.AreEqual(1U, point.Flags);
        Assert.AreEqual(99d, Assert.IsExactInstanceOfType<CommonContract.AttributeDoubleValue>(
            Assert.IsExactInstanceOfType<MetricContract.MetricNumberPointData>(point.Data).Value).Value);
        var write = MetricStorageMapper.ToStorageRows(batch);
        Assert.IsNull(MetricProjection.Project(Assert.ContainsSingle(write.Observations), false).Value);
    }

    [TestMethod]
    public void Histogram_points_keep_their_explicit_bounds_and_aggregates()
    {
        var batch = OtlpConverter.ConvertMetrics(Request(Histogram(
            "http.server.duration",
            T0,
            count: 4,
            sum: 10,
            bounds: [1.0, 5.0],
            counts: [1, 2, 1])));

        var point = Assert.ContainsSingle(batch.Points);
        Assert.AreEqual(MetricKind.Histogram, point.Kind);
        var data = Assert.IsExactInstanceOfType<MetricContract.MetricHistogramPointData>(point.Data);
        Assert.AreEqual(4UL, data.Count);
        Assert.AreEqual(10d, data.Sum);
        Assert.AreSequenceEqual([1.0, 5.0], data.ExplicitBounds);
        Assert.AreSequenceEqual([1UL, 2UL, 1UL], data.BucketCounts);
    }

    [TestMethod]
    public void An_exponential_histogram_retains_native_buckets_and_derives_explicit_bounds()
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

        var point = Assert.ContainsSingle(batch.Points);
        Assert.AreEqual(MetricKind.ExponentialHistogram, point.Kind);
        var data = Assert.IsExactInstanceOfType<MetricContract.MetricExponentialHistogramPointData>(point.Data);
        Assert.AreEqual(0, data.Scale);
        Assert.AreEqual(1UL, data.ZeroCount);
        Assert.AreEqual(0.5d, data.ZeroThreshold);
        Assert.IsNotNull(data.Positive); Assert.IsNotNull(data.Negative);
        Assert.AreEqual(1, data.Positive.Offset); Assert.AreEqual(0, data.Negative.Offset);
        Assert.AreSequenceEqual([1UL, 2UL], data.Positive.BucketCounts);
        Assert.AreSequenceEqual([2UL], data.Negative.BucketCounts);
        var projected = MetricProjection.Project(Assert.ContainsSingle(MetricStorageMapper.ToStorageRows(batch).Observations), false);
        var bounds = System.Text.Json.JsonSerializer.Deserialize(projected.BucketBoundsJson!, StorageJsonSerializerContext.Default.Doubles)!;
        Assert.AreEqual(4, bounds.Count);
        Assert.AreEqual(-1d, bounds[0], 0.0000000005d);
        Assert.AreEqual(0.5d, bounds[1], 0.0000000005d);
        Assert.AreEqual(4d, bounds[2], 0.0000000005d);
        Assert.AreEqual(8d, bounds[3], 0.0000000005d);
        // Counts are always one longer: the trailing element is the implicit +infinity bucket.
        Assert.AreSequenceEqual([2UL, 1UL, 1UL, 2UL, 0UL], System.Text.Json.JsonSerializer.Deserialize(projected.BucketCountsJson!, StorageJsonSerializerContext.Default.Counts)!);
    }

    [TestMethod]
    public void Summary_points_retain_the_original_quantiles_alongside_other_metric_kinds()
    {
        var batch = OtlpConverter.ConvertMetrics(Request(
            Gauge("kept.gauge", (T0, 1d)),
            new OtlpMetric
            {
                Name = "legacy.summary",
                Summary = new Summary
                {
                    DataPoints = { new SummaryDataPoint { TimeUnixNano = T0 }, new SummaryDataPoint { TimeUnixNano = T0 + 1 } }
                }
            }));

        Assert.AreEqual(0L, batch.RejectedDataPoints);
        Assert.IsNull(batch.RejectionMessage);
        Assert.HasCount(3, batch.Points);
        Assert.HasCount(2, batch.Points.Where(static point => point.Data is MetricContract.MetricSummaryPointData));
    }

    [TestMethod]
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

        var attributes = Assert.ContainsSingle(batch.Points).Attributes;
        Assert.AreSequenceEqual(["http.request.method"], attributes.Keys);
    }

    [TestMethod]
    public async Task Metrics_round_trip_through_storage_and_answer_the_catalog_and_series_questions()
    {
        await using var store = new DuckDbStore(":memory:");
        var ct = testContext.CancellationToken;

        await IngestAsync(store, Request(
            GaugeWithRoute("http.server.active", "/a", (T0, 2d)),
            GaugeWithRoute("http.server.active", "/b", (T0, 8d))), ct);

        var catalog = await store.ListMetricsAsync(ProjectScope.DefaultProjectId, ct: ct);
        var entry = Assert.ContainsSingle(catalog);
        Assert.AreEqual("http.server.active", entry.MetricName);
        Assert.AreEqual((byte)MetricKind.Gauge, entry.MetricKind);
        // Two attribute sets under one name are two series, not two metrics.
        Assert.AreEqual(2, entry.SeriesCount);

        var all = await store.ListMetricSeriesAsync(
            ProjectScope.DefaultProjectId, "http.server.active", [], ct: ct);
        Assert.AreEqual(2, all.Count);

        var exact = await store.ListMetricSeriesAsync(
            ProjectScope.DefaultProjectId,
            "http.server.active",
            [new MetricAttributeMatcher("http.route", "/a", IsPrefix: false)],
            ct: ct);
        Assert.ContainsSingle(exact);

        var prefixed = await store.ListMetricSeriesAsync(
            ProjectScope.DefaultProjectId,
            "http.server.active",
            [new MetricAttributeMatcher("http.route", "/", IsPrefix: true)],
            ct: ct);
        Assert.AreEqual(2, prefixed.Count);
    }

    [TestMethod]
    public async Task Re_ingesting_the_same_stream_updates_one_series_instead_of_forking_it()
    {
        await using var store = new DuckDbStore(":memory:");
        var ct = testContext.CancellationToken;

        await IngestAsync(store, Request(Gauge("stable.gauge", (T0, 1d))), ct);
        await IngestAsync(store, Request(Gauge("stable.gauge", (T0 + OneSecond, 2d))), ct);

        var series = Assert.ContainsSingle(await store.ListMetricSeriesAsync(
            ProjectScope.DefaultProjectId, "stable.gauge", [], ct: ct));
        Assert.AreEqual(T0, series.FirstSeenUnixNano);
        Assert.AreEqual(T0 + OneSecond, series.LastSeenUnixNano);
    }

    [TestMethod]
    [DataRow(nameof(MetricAggregation.Avg), 20d)]
    [DataRow(nameof(MetricAggregation.Min), 10d)]
    [DataRow(nameof(MetricAggregation.Max), 30d)]
    [DataRow(nameof(MetricAggregation.Sum), 60d)]
    [DataRow(nameof(MetricAggregation.Count), 3d)]
    [DataRow(nameof(MetricAggregation.Last), 30d)]
    public async Task A_range_query_aggregates_server_side_into_one_bucket(
        string aggregationName,
        double expected)
    {
        var aggregation = Enum.Parse<MetricAggregation>(aggregationName);
        await using var store = new DuckDbStore(":memory:");
        var ct = testContext.CancellationToken;

        await IngestAsync(store, Request(Gauge(
            "queue.depth",
            (T0, 10d),
            (T0 + OneSecond, 20d),
            (T0 + 2 * OneSecond, 30d))), ct);

        var result = await store.QueryMetricAsync(new MetricRangeQuery
        {
            ProjectId = ProjectScope.DefaultProjectId,
            MetricName = "queue.depth",
            StartUnixNano = T0,
            EndUnixNano = T0 + 10 * OneSecond,
            StepUnixNano = 10 * OneSecond,
            Aggregation = aggregation
        }, ct);

        var point = Assert.ContainsSingle(Assert.ContainsSingle(result).Points);
        Assert.AreEqual(T0, point.BucketStartUnixNano);
        Assert.AreEqual(expected, point.Value!.Value, 0.0000000005d);
    }

    [TestMethod]
    public async Task A_range_query_buckets_by_step_and_splits_by_the_requested_attribute()
    {
        await using var store = new DuckDbStore(":memory:");
        var ct = testContext.CancellationToken;

        await IngestAsync(store, Request(
            GaugeWithRoute("http.server.active", "/a", (T0, 1d), (T0 + 5 * OneSecond, 3d)),
            GaugeWithRoute("http.server.active", "/b", (T0, 10d))), ct);

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

        Assert.AreEqual(2, result.Count);
        var routeA = result.Single(static stream => stream.AttributesJson!.Contains("/a", StringComparison.Ordinal));
        Assert.AreEqual(2, routeA.Points.Count);
        Assert.AreEqual(T0, routeA.Points[0].BucketStartUnixNano);
        Assert.AreEqual(1d, routeA.Points[0].Value!.Value, 0.0000000005d);
        Assert.AreEqual(T0 + 5 * OneSecond, routeA.Points[1].BucketStartUnixNano);
        Assert.AreEqual(3d, routeA.Points[1].Value!.Value, 0.0000000005d);

        var routeB = result.Single(static stream => stream.AttributesJson!.Contains("/b", StringComparison.Ordinal));
        Assert.AreEqual(10d, Assert.ContainsSingle(routeB.Points).Value!.Value, 0.0000000005d);
    }

    [TestMethod]
    public async Task A_matcher_narrows_a_range_query_to_the_series_it_selects()
    {
        await using var store = new DuckDbStore(":memory:");
        var ct = testContext.CancellationToken;

        await IngestAsync(store, Request(
            GaugeWithRoute("http.server.active", "/a", (T0, 4d)),
            GaugeWithRoute("http.server.active", "/b", (T0, 100d))), ct);

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

        Assert.AreEqual(4d, Assert.ContainsSingle(Assert.ContainsSingle(result).Points).Value!.Value, 0.0000000005d);
    }

    [TestMethod]
    public async Task A_histogram_query_reads_the_recorded_aggregates_and_interpolates_percentiles()
    {
        await using var store = new DuckDbStore(":memory:");
        var ct = testContext.CancellationToken;

        // Two points in one step, buckets (-inf,1] (1,5] (5,inf): counts 1,2,1 and 1,2,1
        // fold to 2,4,2, so the median falls in (1,5] at exactly its midpoint.
        await IngestAsync(store, Request(Histogram(
            "http.server.duration", T0, count: 4, sum: 10, bounds: [1.0, 5.0], counts: [1, 2, 1])), ct);
        await IngestAsync(store, Request(Histogram(
            "http.server.duration", T0 + OneSecond, count: 4, sum: 30, bounds: [1.0, 5.0], counts: [1, 2, 1])), ct);

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
        var average = Assert.ContainsSingle(Assert.ContainsSingle(await store.QueryMetricAsync(request, ct)).Points);
        Assert.AreEqual(5d, average.Value!.Value, 0.0000000005d);

        var count = Assert.ContainsSingle(Assert.ContainsSingle(await store.QueryMetricAsync(
            request with { Aggregation = MetricAggregation.Count }, ct)).Points);
        Assert.AreEqual(8d, count.Value!.Value, 0.0000000005d);

        var median = Assert.ContainsSingle(Assert.ContainsSingle(await store.QueryMetricAsync(
            request with { Aggregation = MetricAggregation.P50 }, ct)).Points);
        Assert.AreEqual(3d, median.Value!.Value, 0.0000000005d);

        var p99 = Assert.ContainsSingle(Assert.ContainsSingle(await store.QueryMetricAsync(
            request with { Aggregation = MetricAggregation.P99 }, ct)).Points);
        // The top bucket has no finite upper edge, so the last finite bound is reported.
        Assert.AreEqual(5d, p99.Value!.Value, 0.0000000005d);
    }

    [TestMethod]
    public async Task Retention_deletes_expired_points_and_the_series_they_leave_empty()
    {
        await using var store = new DuckDbStore(":memory:");
        var ct = testContext.CancellationToken;

        await IngestAsync(store, Request(
            Gauge("old.gauge", (T0, 1d)),
            Gauge("new.gauge", (T0 + 100 * OneSecond, 2d))), ct);

        var deleted = await store.DeleteExpiredMetricsBatchAsync(T0 + OneSecond, 100, ct);
        Assert.AreEqual(1, deleted.Points);
        Assert.AreEqual(1, deleted.Series);

        var remaining = await store.ListMetricsAsync(ProjectScope.DefaultProjectId, ct: ct);
        Assert.AreEqual("new.gauge", Assert.ContainsSingle(remaining).MetricName);
    }

    [TestMethod]
    public async Task Product_and_analysis_histograms_share_interval_math_and_ingested_exemplars()
    {
        await using var store = new DuckDbStore(":memory:");
        var ct = testContext.CancellationToken;
        const string TraceId = "00000000000000000000000000000042";
        var first = new HistogramDataPoint
        {
            StartTimeUnixNano = T0, TimeUnixNano = T0 + OneSecond, Count = 10, Sum = 100,
            ExplicitBounds = { 20d }, BucketCounts = { 10UL, 0UL }
        };
        var second = first.Clone();
        second.TimeUnixNano = T0 + 2 * OneSecond; second.Count = 15; second.Sum = 160;
        second.BucketCounts[0] = 15;
        second.Exemplars.Add(new Exemplar { TraceId = Google.Protobuf.ByteString.CopyFrom(Convert.FromHexString(TraceId)), TimeUnixNano = second.TimeUnixNano, AsDouble = 12 });
        await IngestAsync(store, Request(new OtlpMetric
        {
            Name = "cumulative.latency", Histogram = new Histogram
            { AggregationTemporality = AggregationTemporality.Cumulative, DataPoints = { first, second } }
        }), ct);
        var query = new MetricRangeQuery
        {
            ProjectId = ProjectScope.DefaultProjectId, MetricName = "cumulative.latency",
            StartUnixNano = T0, EndUnixNano = T0 + 3 * OneSecond, StepUnixNano = 3 * OneSecond,
            Aggregation = MetricAggregation.Count
        };
        Assert.AreEqual(15d, Assert.ContainsSingle(Assert.ContainsSingle(await store.QueryMetricAsync(query, ct)).Points).Value);
        // The previous point is outside the selected window but remains the subtraction baseline.
        Assert.AreEqual(5d, Assert.ContainsSingle(Assert.ContainsSingle(await store.QueryMetricAsync(query with { StartUnixNano = T0 + OneSecond + 1 }, ct)).Points).Value);
        var snapshot = await store.ReadAnalysisSnapshotAsync(ProjectScope.DefaultProjectId, ct: ct);
        Assert.AreEqual(TraceId, Assert.ContainsSingle(snapshot.Exemplars).TraceId);
        Assert.AreEqual(5UL, MetricHistogram.HistogramDelta(snapshot.Points.OrderBy(p => p.TimeUnixNano).Last(), snapshot.Points.OrderBy(p => p.TimeUnixNano).First(), T0)!.HistogramCount);
    }

    private static async Task IngestAsync(
        DuckDbStore store,
        ExportMetricsServiceRequest request,
        CancellationToken ct)
    {
        var write = MetricStorageMapper.ToStorageRows(OtlpConverter.ConvertMetrics(request));
        await store.InsertMetricsAsync(write.Series, write.Observations, ct);
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
