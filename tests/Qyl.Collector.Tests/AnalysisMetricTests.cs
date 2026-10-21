using System.Text.Json;
using OpenTelemetry.Proto.Collector.Metrics.V1;
using OpenTelemetry.Proto.Metrics.V1;
using OpenTelemetry.Proto.Resource.V1;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Storage;
using static Qyl.Collector.Tests.CollectorApiTestHost;
using OtlpMetric = OpenTelemetry.Proto.Metrics.V1.Metric;

namespace Qyl.Collector.Tests;

/// <summary>
/// query_metric answers only what the recorded points establish: the latest point by its timestamp,
/// the interval a cumulative counter recorded, a percentile inside the recorded bounds and extrema,
/// and an unknown where a measurement is missing.
/// </summary>
public sealed partial class AnalysisApiTests
{
    [TestMethod]
    public async Task Last_is_the_latest_point_across_series_and_unknown_when_simultaneous_points_disagree()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var late = FixtureUnixNano - 5 * Minute; var early = FixtureUnixNano - 55 * Minute;
        await InsertAsync(host, Gauge("queue.depth", "s_late", late), (late, 100d));
        await InsertAsync(host, Gauge("queue.depth", "s_early", early), (early, 200d));
        await InsertAsync(host, Gauge("pool.size", "p_a", late), (late, 3d));
        await InsertAsync(host, Gauge("pool.size", "p_b", late), (late, 4d));

        Assert.AreEqual(100d, Bucket(await Read(host, "query_metric", """{"metric_name":"queue.depth","aggregation":"last","step_ms":3600000}""")).GetDouble());
        Assert.AreEqual(JsonValueKind.Null, Bucket(await Read(host, "query_metric", """{"metric_name":"pool.size","aggregation":"last","step_ms":3600000}""")).ValueKind);
    }

    /// <summary>
    /// A metric query without series names why: a metric the project never recorded, one whose last point precedes
    /// the window, and filters that exclude every series the window holds each answer differently.
    /// </summary>
    [TestMethod]
    public async Task A_metric_query_without_series_names_why()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var earlier = FixtureUnixNano - 180 * Minute; var recent = FixtureUnixNano - 5 * Minute;
        await InsertAsync(host, Gauge("queue.depth", "s_earlier", earlier), (earlier, 1d));
        await InsertAsync(host, Gauge("pool.size", "s_recent", recent), (recent, 2d));

        foreach (var (input, detail) in new[]
                 {
                     ("""{"metric_name":"never.recorded"}""", "Metric never.recorded was not found in this project"),
                     ("""{"metric_name":"queue.depth"}""", "Metric queue.depth has no points in this window; its last point is at "
                         + FixtureUtcNow.AddMinutes(-180).ToString("O", System.Globalization.CultureInfo.InvariantCulture)),
                     ("""{"metric_name":"pool.size","matchers":[{"key":"instance","op":"eq","value":"s_other"}]}""", "No series of metric pool.size in this window matches")
                 })
        {
            using var response = await host.SendAsync(HttpMethod.Post, "/api/v1/analysis/query_metric", ReaderApiKey, input);
            var error = await host.JsonAsync(response);
            Assert.AreEqual(System.Net.HttpStatusCode.NotFound, response.StatusCode, error.GetRawText());
            Assert.Contains(detail, error.GetProperty("detail").GetString()!, StringComparison.Ordinal);
        }
    }

    [TestMethod]
    public async Task A_ratio_of_an_exact_numerator_series_finds_its_denominator_metric()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var time = FixtureUnixNano - 30 * Minute;
        await InsertAsync(host, Gauge("numerator", "ms_numerator", time), (time, 10d));
        await InsertAsync(host, Gauge("denominator", "ms_denominator", time), (time, 20d));

        var ratio = await Read(host, "query_metric", """{"metric_name":"numerator","series_id":"ms_numerator","aggregation":"sum","step_ms":3600000,"ratio_of":{"metric_name":"denominator"}}""");

        Assert.AreEqual(.5, Bucket(ratio).GetDouble());
        Assert.AreEqual("1", ratio.GetProperty("unit").GetString());
    }

    /// <summary>
    /// A cumulative counter restarted at its recorded start time; its value accrued over the interval
    /// since then, which is also the only interval a first point can claim. A decrease without a
    /// recorded start has no known interval, so it has no rate.
    /// </summary>
    [TestMethod]
    public async Task Counter_rates_use_the_recorded_cumulative_interval_and_never_infer_one()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var restarted = FixtureUnixNano - 9 * Minute;
        await InsertAsync(host, Counter("restart.counter", "c_restart"),
            (FixtureUnixNano - 60 * Minute, FixtureUnixNano - 120 * Minute, 100d), (restarted, restarted - Minute, 60d));
        await InsertAsync(host, Counter("first.counter", "c_first"), (restarted, restarted - Minute, 60d));
        await InsertAsync(host, Counter("unrecorded.counter", "c_unrecorded"),
            (FixtureUnixNano - 20 * Minute, FixtureUnixNano - 60 * Minute, 100d), (FixtureUnixNano - 10 * Minute, FixtureUnixNano - 60 * Minute, 30d));

        Assert.AreEqual(1d, Bucket(await Read(host, "query_metric", """{"metric_name":"restart.counter","lookback_minutes":30,"function":"rate","aggregation":"sum","step_ms":3600000}""")).GetDouble(), 1e-9);
        Assert.AreEqual(1d, Bucket(await Read(host, "query_metric", """{"metric_name":"first.counter","function":"rate","aggregation":"sum","step_ms":3600000}""")).GetDouble(), 1e-9);
        Assert.AreEqual(60d, Bucket(await Read(host, "query_metric", """{"metric_name":"first.counter","function":"increase","aggregation":"sum","step_ms":3600000}""")).GetDouble());
        Assert.AreEqual(JsonValueKind.Null, Bucket(await Read(host, "query_metric", """{"metric_name":"unrecorded.counter","lookback_minutes":30,"function":"rate","aggregation":"sum","step_ms":3600000}""")).ValueKind);
        Assert.AreEqual(30d, Bucket(await Read(host, "query_metric", """{"metric_name":"unrecorded.counter","lookback_minutes":30,"function":"increase","aggregation":"sum","step_ms":3600000}""")).GetDouble());
    }

    /// <summary>
    /// Every observation of a negative histogram lies in [-20, -12]: the first bucket has no lower
    /// bound but the recorded minimum, and the recorded maximum caps it.
    /// </summary>
    [TestMethod]
    [DataRow("p50", "[-10,-5,0]", "[100,0,0,0]", 100UL, -20d, -12d, -16d)]
    [DataRow("p95", "[-10,0]", "[10,0,0]", 10UL, -20d, -10d, -10.5d)]
    public async Task Histogram_percentiles_stay_inside_the_recorded_bounds_and_extrema(string percentile, string bounds, string counts, ulong count, double min, double max, double expected)
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var time = FixtureUnixNano - 10 * Minute;
        await InsertHistogramAsync(host, "temperature", 1, new MetricPointRow
        {
            ProjectId = "alpha", SeriesId = "temperature", TimeUnixNano = time, StartTimeUnixNano = time - Minute,
            HistogramCount = count, HistogramSum = -16d * count, HistogramMin = min, HistogramMax = max, BucketBoundsJson = bounds, BucketCountsJson = counts
        });

        var value = Bucket(await Read(host, "query_metric", $$"""{"metric_name":"temperature","aggregation":"{{percentile}}","step_ms":3600000}""")).GetDouble();

        Assert.AreEqual(expected, value, 1e-9);
        Assert.IsInRange(min, max, value);
    }

    /// <summary>
    /// Without a recorded minimum the first bucket has no lower bound, so a percentile inside it is unknown.
    /// </summary>
    [TestMethod]
    public async Task A_percentile_in_an_unbounded_bucket_without_a_recorded_extremum_is_unknown()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var time = FixtureUnixNano - 10 * Minute;
        await InsertHistogramAsync(host, "latency", 1, new MetricPointRow
        {
            ProjectId = "alpha", SeriesId = "latency", TimeUnixNano = time, StartTimeUnixNano = time - Minute,
            HistogramCount = 10, HistogramSum = 30, BucketBoundsJson = "[5,10]", BucketCountsJson = "[10,0,0]"
        });

        Assert.AreEqual(JsonValueKind.Null, Bucket(await Read(host, "query_metric", """{"metric_name":"latency","aggregation":"p50","step_ms":3600000}""")).ValueKind);
    }

    /// <summary>
    /// A histogram reports only measurements its points recorded: an absent sum is not zero, a point
    /// whose minimum is unknown leaves the minimum unknown, and a NO_RECORDED_VALUE point is missing
    /// data rather than a count of zero.
    /// </summary>
    [TestMethod]
    public async Task Histogram_reducers_keep_missing_measurements_unknown()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var time = FixtureUnixNano - 30 * Minute;
        var noSum = new MetricPointRow
        {
            ProjectId = "alpha", SeriesId = "no_sum", TimeUnixNano = time, StartTimeUnixNano = time - Minute,
            HistogramCount = 2, HistogramMin = 5, HistogramMax = 10, BucketBoundsJson = "[10]", BucketCountsJson = "[2,0]"
        };
        await InsertHistogramAsync(host, "no.sum", 1, noSum);
        await InsertHistogramAsync(host, "known.sum", 1, noSum with { SeriesId = "known_sum", HistogramSum = 15 });
        await InsertHistogramAsync(host, "partial.min", 1,
            noSum with { SeriesId = "partial_min", HistogramMin = 10, HistogramMax = 20, HistogramSum = 30, BucketBoundsJson = "[20]" },
            noSum with { SeriesId = "partial_min", TimeUnixNano = time + 1_000_000, StartTimeUnixNano = time, HistogramMin = null, HistogramMax = 5, BucketBoundsJson = "[5]" });
        await IngestHistogramAsync(host, "unrecorded", AggregationTemporality.Delta,
            new HistogramDataPoint { StartTimeUnixNano = time - Minute, TimeUnixNano = time, Flags = 1 });

        double? Value(JsonElement result) => Bucket(result) is { ValueKind: JsonValueKind.Number } number ? number.GetDouble() : null;
        Task<JsonElement> Query(string metric, string aggregation) =>
            Read(host, "query_metric", $$"""{"metric_name":"{{metric}}","aggregation":"{{aggregation}}","step_ms":3600000}""");

        Assert.IsNull(Value(await Query("no.sum", "sum")));
        Assert.IsNull(Value(await Query("no.sum", "avg")));
        Assert.AreEqual(2d, Value(await Query("no.sum", "count")));
        Assert.AreEqual(7.5d, Value(await Query("known.sum", "avg")));
        Assert.IsNull(Value(await Query("partial.min", "min")));
        Assert.AreEqual(20d, Value(await Query("partial.min", "max")));
        Assert.IsNull(Value(await Query("unrecorded", "count")));
    }

    /// <summary>
    /// A NO_RECORDED_VALUE point inside a cumulative histogram carries no distribution; the next recorded
    /// point is measured against the last recorded one, so no interval is lost.
    /// </summary>
    [TestMethod]
    public async Task A_cumulative_histogram_measures_across_a_point_without_a_recorded_value()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var start = FixtureUnixNano - 60 * Minute;
        HistogramDataPoint Point(ulong minutes, ulong count) => new()
        {
            StartTimeUnixNano = start, TimeUnixNano = start + minutes * Minute, Count = count, Sum = count,
            ExplicitBounds = { 10 }, BucketCounts = { count, 0 }
        };
        await IngestHistogramAsync(host, "cumulative.latency", AggregationTemporality.Cumulative,
            Point(10, 10), new HistogramDataPoint { StartTimeUnixNano = start, TimeUnixNano = start + 20 * Minute, Flags = 1 }, Point(30, 15));

        Assert.AreEqual(15d, Bucket(await Read(host, "query_metric", """{"metric_name":"cumulative.latency","aggregation":"count","step_ms":3600000}""")).GetDouble());
    }

    /// <summary>
    /// A histogram's rate is its count or sum per second of the interval its points recorded: 60
    /// observations over 60 seconds are 1/s for delta and cumulative temporality alike. A count is
    /// observations, never the metric's unit, and no other aggregation has a rate.
    /// </summary>
    [TestMethod]
    public async Task A_histogram_rate_divides_its_count_or_sum_by_the_recorded_interval()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var time = FixtureUnixNano - 30 * Minute;
        await InsertHistogramAsync(host, "delta.events", 1, "{event}", Observations("delta", time - Minute, (time, 60)));
        await InsertHistogramAsync(host, "cumulative.events", 2, "{event}", Observations("cumulative", time - 240 * Minute, (time - Minute, 100), (time, 160)));

        foreach (var metric in new[] { "delta.events", "cumulative.events" })
        {
            var count = await Read(host, "query_metric", $$"""{"metric_name":"{{metric}}","function":"rate","aggregation":"count","step_ms":3600000}""");
            Assert.AreEqual(1d, Bucket(count).GetDouble(), 1e-9, metric);
            Assert.AreEqual("1/s", count.GetProperty("unit").GetString());
            var sum = await Read(host, "query_metric", $$"""{"metric_name":"{{metric}}","function":"rate","aggregation":"sum","step_ms":3600000}""");
            Assert.AreEqual(2d, Bucket(sum).GetDouble(), 1e-9, metric);
            Assert.AreEqual("{event}/s", sum.GetProperty("unit").GetString());
        }
        var observations = await Read(host, "query_metric", """{"metric_name":"delta.events","aggregation":"count","step_ms":3600000}""");
        Assert.AreEqual(60d, Bucket(observations).GetDouble());
        Assert.AreEqual("1", observations.GetProperty("unit").GetString());

        using var rejected = await host.SendAsync(HttpMethod.Post, "/api/v1/analysis/query_metric", ReaderApiKey,
            """{"metric_name":"delta.events","function":"rate","aggregation":"p95","step_ms":3600000}""");
        var json = await host.JsonAsync(rejected);
        Assert.AreEqual(System.Net.HttpStatusCode.BadRequest, rejected.StatusCode, json.GetRawText());
        var error = Assert.ContainsSingle(json.GetProperty("errors").EnumerateArray());
        Assert.AreEqual("function", error.GetProperty("field").GetString());
        Assert.AreEqual("function.unsupported", error.GetProperty("code").GetString());
    }

    /// <summary>
    /// The previous window's first cumulative point is measured against the point before it, which
    /// precedes both windows: the read fetches each series' last recorded point before its start.
    /// </summary>
    [TestMethod]
    public async Task A_previous_window_comparison_measures_its_first_cumulative_point_from_the_stored_predecessor()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var start = FixtureUnixNano - 240 * Minute;
        (ulong Time, ulong Count)[] samples = [(FixtureUnixNano - 121 * Minute, 100), (FixtureUnixNano - 90 * Minute, 160), (FixtureUnixNano - 30 * Minute, 220)];
        await InsertHistogramAsync(host, "comparison.events", 2, "{event}", Observations("comparison", start, samples));
        await InsertAsync(host, Counter("comparison.counter", "c_comparison"), samples.Select(sample => (sample.Time, (ulong?)start, (double)sample.Count)).ToArray());

        foreach (var query in new[]
                 {
                     """{"metric_name":"comparison.events","aggregation":"count","step_ms":3600000,"baseline":"previous_window"}""",
                     """{"metric_name":"comparison.counter","function":"increase","aggregation":"sum","step_ms":3600000,"baseline":"previous_window"}"""
                 })
        {
            var result = await Read(host, "query_metric", query);
            Assert.AreEqual(60d, Bucket(result).GetDouble(), query);
            var previous = Assert.ContainsSingle(Assert.ContainsSingle(result.GetProperty("comparison").GetProperty("series").EnumerateArray()).GetProperty("buckets").EnumerateArray());
            Assert.AreEqual(60d, previous.GetProperty("value").GetDouble(), query);
        }
    }

    /// <summary>
    /// Without step_ms a day splits into at most 60 buckets. Only buckets that hold points come back, and
    /// each series carries the aggregation over the whole window.
    /// </summary>
    [TestMethod]
    public async Task Query_metric_returns_occupied_buckets_and_one_value_for_the_window()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        await InsertAsync(host, Gauge("queue.depth", "q_depth", FixtureUnixNano - 10 * Minute), (FixtureUnixNano - 50 * Minute, 10d), (FixtureUnixNano - 10 * Minute, 30d));

        var result = await Read(host, "query_metric", """{"metric_name":"queue.depth","aggregation":"avg","lookback_minutes":1440}""");

        Assert.AreEqual(1_440_000, result.GetProperty("step_ms").GetInt64());
        var series = Assert.ContainsSingle(result.GetProperty("series").EnumerateArray());
        Assert.AreEqual(20d, series.GetProperty("value").GetDouble());
        Assert.AreSequenceEqual([10d, 30d], series.GetProperty("buckets").EnumerateArray().Select(static b => b.GetProperty("value").GetDouble()));
    }

    private static readonly string[] s_catalogRowKeys = ["name", "kind", "temporality", "services", "series_count", "last_seen", "supported_aggregations", "has_conflict", "keys", "suggested_group_by"];

    /// <summary>list_metrics names each instrument in one compact row, fifty to a page by default.</summary>
    [TestMethod]
    public async Task List_metrics_returns_compact_rows_fifty_by_default()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var time = FixtureUnixNano - 10 * Minute;
        foreach (var index in Enumerable.Range(0, 60))
            await InsertAsync(host, Gauge($"metric.{index:D3}", $"m_{index:D3}", time), (time, index));

        var result = await Read(host, "list_metrics", "{}");

        var items = result.GetProperty("items").EnumerateArray().ToArray();
        Assert.HasCount(50, items);
        Assert.IsTrue(result.GetProperty("has_more").GetBoolean());
        Assert.AreEquivalent(s_catalogRowKeys, items[0].EnumerateObject().Select(static p => p.Name).ToArray());
        Assert.AreEqual(("metric.000", "gauge", 1), (items[0].GetProperty("name").GetString(), items[0].GetProperty("kind").GetString(), items[0].GetProperty("series_count").GetInt32()));
    }

    private static MetricPointRow[] Observations(string seriesId, ulong start, params (ulong Time, ulong Count)[] samples) =>
    [
        .. samples.Select(sample => new MetricPointRow
        {
            ProjectId = "alpha", SeriesId = seriesId, TimeUnixNano = sample.Time, StartTimeUnixNano = start, HistogramCount = sample.Count, HistogramSum = 2d * sample.Count,
            HistogramMin = 2, HistogramMax = 2, BucketBoundsJson = "[2]", BucketCountsJson = "[" + sample.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",0]"
        })
    ];

    private static JsonElement Bucket(JsonElement result) =>
        Assert.ContainsSingle(Assert.ContainsSingle(result.GetProperty("series").EnumerateArray()).GetProperty("buckets").EnumerateArray()).GetProperty("value");

    private static MetricSeriesRow Gauge(string name, string seriesId, ulong lastSeen) => new()
    {
        ProjectId = "alpha", SeriesId = seriesId, MetricName = name, MetricKind = 1, Temporality = 0, IsMonotonic = 0,
        FirstSeenUnixNano = lastSeen, LastSeenUnixNano = lastSeen, AttributesJson = "{\"instance\":\"" + seriesId + "\"}"
    };

    private static MetricSeriesRow Counter(string name, string seriesId) => new()
    {
        ProjectId = "alpha", SeriesId = seriesId, MetricName = name, MetricKind = 2, Temporality = 2, IsMonotonic = 1, Unit = "{event}",
        FirstSeenUnixNano = FixtureUnixNano - 120 * Minute, LastSeenUnixNano = FixtureUnixNano - Minute
    };

    private async Task InsertAsync(CollectorApiTestHost host, MetricSeriesRow series, params (ulong Time, double Value)[] points) =>
        await host.Store.InsertMetricsAsync([series], [.. points.Select(point => MetricFixtures.Observation(series,
            new MetricPointRow { ProjectId = "alpha", SeriesId = series.SeriesId, TimeUnixNano = point.Time, Value = point.Value }))], testContext.CancellationToken);

    private async Task InsertAsync(CollectorApiTestHost host, MetricSeriesRow series, params (ulong Time, ulong? Start, double Value)[] points) =>
        await host.Store.InsertMetricsAsync([series], [.. points.Select(point => MetricFixtures.Observation(series,
            new MetricPointRow { ProjectId = "alpha", SeriesId = series.SeriesId, TimeUnixNano = point.Time, StartTimeUnixNano = point.Start, Value = point.Value }))], testContext.CancellationToken);

    private Task InsertHistogramAsync(CollectorApiTestHost host, string name, byte temporality, params MetricPointRow[] points) =>
        InsertHistogramAsync(host, name, temporality, null, points);

    private async Task InsertHistogramAsync(CollectorApiTestHost host, string name, byte temporality, string? unit, params MetricPointRow[] points)
    {
        var series = new MetricSeriesRow
        {
            ProjectId = "alpha", SeriesId = points[0].SeriesId, MetricName = name, MetricKind = 3, Temporality = temporality, IsMonotonic = 0, Unit = unit,
            FirstSeenUnixNano = points.Min(static point => point.TimeUnixNano), LastSeenUnixNano = points.Max(static point => point.TimeUnixNano)
        };
        await host.Store.InsertMetricsAsync([series], [.. points.Select(point => MetricFixtures.Observation(series, point))], testContext.CancellationToken);
    }

    private async Task IngestHistogramAsync(CollectorApiTestHost host, string name, AggregationTemporality temporality, params HistogramDataPoint[] points)
    {
        var histogram = new Histogram { AggregationTemporality = temporality };
        histogram.DataPoints.Add(points);
        var request = new ExportMetricsServiceRequest
        {
            ResourceMetrics = { new ResourceMetrics { Resource = new Resource(), ScopeMetrics = { new ScopeMetrics { Metrics = { new OtlpMetric { Name = name, Histogram = histogram } } } } } }
        };
        var rows = MetricStorageMapper.ToStorageRows(OtlpConverter.ConvertMetrics(request), "alpha");
        await host.Store.InsertMetricsAsync(rows.Series, rows.Observations, testContext.CancellationToken);
    }
}
