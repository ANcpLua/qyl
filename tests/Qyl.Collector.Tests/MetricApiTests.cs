using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Text.Json;
using OpenTelemetry.Proto.Collector.Metrics.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Metrics.V1;
using OpenTelemetry.Proto.Resource.V1;
using Qyl.Collector;
using Qyl.Collector.Hosting;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Storage;
using OtlpMetric = OpenTelemetry.Proto.Metrics.V1.Metric;

namespace Qyl.Collector.Tests;

public sealed class MetricApiTests
{
    private const ulong T0 = 1_700_000_000_000_000_000UL;
    private const ulong OneSecond = 1_000_000_000UL;
    private const string T0Iso = "2023-11-14T22:13:20Z";
    private const string T0PlusMinuteIso = "2023-11-14T22:14:20Z";

    [Fact]
    public async Task The_catalog_route_lists_metric_names_with_their_shape()
    {
        await using var store = new DuckDbStore(":memory:");
        var ct = TestContext.Current.CancellationToken;
        await IngestAsync(store, ct,
            Gauge("http.server.active", "/a", (T0, 2d)),
            Gauge("http.server.active", "/b", (T0, 8d)),
            Counter("http.server.requests", (T0, 42d)));

        var context = CreateContext();
        var result = await CollectorEndpointExtensions.GetMetricsAsync(context, store, ct);
        await result.ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        var body = await ReadBodyAsync(context);
        var page = JsonSerializer.Deserialize(body, QylSerializerContext.Default.CursorPageMetricDescriptor)!;

        Assert.Equal(2, page.Items.Count);
        var active = page.Items.Single(static item => item.Name is "http.server.active");
        // Two attribute sets under one name are two series, not two metrics.
        Assert.Equal(2, active.SeriesCount);
        Assert.Equal(Qyl.Api.Contracts.OTel.Metrics.MetricKind.Gauge, active.Kind);
        Assert.False(active.Monotonic);

        var requests = page.Items.Single(static item => item.Name is "http.server.requests");
        Assert.Equal(Qyl.Api.Contracts.OTel.Metrics.MetricKind.Sum, requests.Kind);
        Assert.Equal(Qyl.Api.Contracts.OTel.Metrics.MetricTemporality.Cumulative, requests.Temporality);
        Assert.True(requests.Monotonic);
        Assert.Equal("{request}", requests.Unit);
        Assert.False(page.HasMore);
    }

    [Fact]
    public async Task The_catalog_route_honours_the_contract_name_prefix_parameter()
    {
        await using var store = new DuckDbStore(":memory:");
        var ct = TestContext.Current.CancellationToken;
        await IngestAsync(store, ct,
            Gauge("http.server.active", "/a", (T0, 1d)),
            Gauge("db.client.connections", "/a", (T0, 1d)));

        var context = CreateContext("name_prefix=http.");
        var result = await CollectorEndpointExtensions.GetMetricsAsync(context, store, ct);
        await result.ExecuteAsync(context);

        var page = JsonSerializer.Deserialize(
            await ReadBodyAsync(context),
            QylSerializerContext.Default.CursorPageMetricDescriptor)!;
        Assert.Equal("http.server.active", Assert.Single(page.Items).Name);
    }

    [Fact]
    public async Task The_series_route_lists_streams_and_filters_them_by_attribute()
    {
        await using var store = new DuckDbStore(":memory:");
        var ct = TestContext.Current.CancellationToken;
        await IngestAsync(store, ct,
            Gauge("http.server.active", "/a", (T0, 2d)),
            Gauge("http.server.active", "/b", (T0, 8d)));

        var all = CreateContext();
        await (await CollectorEndpointExtensions.GetMetricSeriesAsync(
            all, "http.server.active", store, ct)).ExecuteAsync(all);
        var allPage = JsonSerializer.Deserialize(
            await ReadBodyAsync(all), QylSerializerContext.Default.CursorPageMetricSeries)!;
        Assert.Equal(2, allPage.Items.Count);
        Assert.All(allPage.Items, static item => Assert.StartsWith("ms_", item.SeriesId, StringComparison.Ordinal));
        Assert.All(allPage.Items, static item =>
            Assert.Contains(item.Attributes!, static a => a.Key is "http.route"));

        var exact = CreateContext("attr=http.route%3D/a");
        await (await CollectorEndpointExtensions.GetMetricSeriesAsync(
            exact, "http.server.active", store, ct)).ExecuteAsync(exact);
        var exactPage = JsonSerializer.Deserialize(
            await ReadBodyAsync(exact), QylSerializerContext.Default.CursorPageMetricSeries)!;
        var only = Assert.Single(exactPage.Items);
        Assert.Equal(
            "/a",
            AttributeString(Assert.Single(only.Attributes!, static a => a.Key is "http.route")));

        var prefixed = CreateContext("attr_prefix=http.route%3D/");
        await (await CollectorEndpointExtensions.GetMetricSeriesAsync(
            prefixed, "http.server.active", store, ct)).ExecuteAsync(prefixed);
        var prefixedPage = JsonSerializer.Deserialize(
            await ReadBodyAsync(prefixed), QylSerializerContext.Default.CursorPageMetricSeries)!;
        Assert.Equal(2, prefixedPage.Items.Count);
    }

    [Fact]
    public async Task An_unrecorded_metric_name_is_a_not_found_on_both_series_and_query()
    {
        await using var store = new DuckDbStore(":memory:");
        var ct = TestContext.Current.CancellationToken;
        await IngestAsync(store, ct, Gauge("http.server.active", "/a", (T0, 1d)));

        var series = CreateContext();
        await (await CollectorEndpointExtensions.GetMetricSeriesAsync(
            series, "never.recorded", store, ct)).ExecuteAsync(series);
        Assert.Equal(StatusCodes.Status404NotFound, series.Response.StatusCode);

        var query = CreateContext($"start_time={T0Iso}&end_time={T0PlusMinuteIso}");
        await (await CollectorEndpointExtensions.QueryMetricAsync(
            query, "never.recorded", store, ct)).ExecuteAsync(query);
        Assert.Equal(StatusCodes.Status404NotFound, query.Response.StatusCode);
    }

    [Fact]
    public async Task The_query_route_aggregates_into_buckets_and_splits_by_group_by()
    {
        await using var store = new DuckDbStore(":memory:");
        var ct = TestContext.Current.CancellationToken;
        await IngestAsync(store, ct,
            Gauge("http.server.active", "/a", (T0, 1d), (T0 + 30 * OneSecond, 3d)),
            Gauge("http.server.active", "/b", (T0, 10d)));

        // step_ms=30000 splits the minute into two buckets; group_by splits the routes.
        var context = CreateContext(
            $"start_time={T0Iso}&end_time={T0PlusMinuteIso}&step_ms=30000&aggregation=avg&group_by=http.route");
        await (await CollectorEndpointExtensions.QueryMetricAsync(
            context, "http.server.active", store, ct)).ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        var result = JsonSerializer.Deserialize(
            await ReadBodyAsync(context), QylSerializerContext.Default.MetricQueryResult)!;

        Assert.Equal("http.server.active", result.Name);
        Assert.Equal(Qyl.Api.Contracts.OTel.Metrics.MetricAggregation.Avg, result.Aggregation);
        Assert.Equal(30_000, result.StepMs);
        Assert.False(result.Truncated);
        Assert.Equal(2, result.Series.Count);

        var routeA = result.Series.Single(stream =>
            stream.Attributes!.Any(static a => a.Key is "http.route" && AttributeString(a) is "/a"));
        Assert.Equal(2, routeA.Buckets.Count);
        Assert.Equal(1d, BucketValue(routeA.Buckets[0]), 9);
        Assert.Equal(3d, BucketValue(routeA.Buckets[1]), 9);
        Assert.Equal(1, routeA.Buckets[0].PointCount);
        // The second bucket starts exactly one step after the range start.
        Assert.Equal(
            routeA.Buckets[0].BucketStart.AddMilliseconds(30_000),
            routeA.Buckets[1].BucketStart);

        var routeB = result.Series.Single(stream =>
            stream.Attributes!.Any(static a => a.Key is "http.route" && AttributeString(a) is "/b"));
        Assert.Equal(10d, BucketValue(Assert.Single(routeB.Buckets)), 9);
    }

    [Fact]
    public async Task The_query_route_interpolates_a_histogram_percentile_over_the_folded_buckets()
    {
        await using var store = new DuckDbStore(":memory:");
        var ct = TestContext.Current.CancellationToken;

        // Buckets (-inf,1] (1,5] (5,inf) with counts 1,2,1 twice fold to 2,4,2, so the
        // median lands in (1,5] at its midpoint.
        await IngestAsync(store, ct, Histogram("http.server.duration", T0));
        await IngestAsync(store, ct, Histogram("http.server.duration", T0 + OneSecond));

        var context = CreateContext(
            $"start_time={T0Iso}&end_time={T0PlusMinuteIso}&step_ms=60000&aggregation=p50");
        await (await CollectorEndpointExtensions.QueryMetricAsync(
            context, "http.server.duration", store, ct)).ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        var result = JsonSerializer.Deserialize(
            await ReadBodyAsync(context), QylSerializerContext.Default.MetricQueryResult)!;

        Assert.Equal(Qyl.Api.Contracts.OTel.Metrics.MetricKind.Histogram, result.Kind);
        Assert.Equal(Qyl.Api.Contracts.OTel.Metrics.MetricAggregation.P50, result.Aggregation);
        Assert.Equal("s", result.Unit);
        var bucket = Assert.Single(Assert.Single(result.Series).Buckets);
        Assert.Equal(3d, BucketValue(bucket), 9);
        Assert.Equal(8, bucket.PointCount);
    }

    [Theory]
    [InlineData("", "start_time")]
    [InlineData("start_time=2023-11-14T22:13:20Z", "end_time")]
    [InlineData("start_time=2023-11-14T22:14:20Z&end_time=2023-11-14T22:13:20Z", "end_time")]
    [InlineData("start_time=2023-11-14T22:13:20Z&end_time=2023-11-14T22:14:20Z&step_ms=0", "step_ms")]
    [InlineData("start_time=2023-11-14T22:13:20Z&end_time=2023-11-14T22:14:20Z&step_ms=86400001", "step_ms")]
    [InlineData("start_time=2023-11-14T22:13:20Z&end_time=2023-11-14T22:14:20Z&series_limit=501", "series_limit")]
    [InlineData("start_time=2023-11-14T22:13:20Z&end_time=2023-11-14T22:14:20Z&aggregation=median", "aggregation")]
    [InlineData("start_time=2023-11-14T22:13:20Z&end_time=2023-11-14T22:14:20Z&attr=noequalssign", "attr")]
    public async Task The_query_route_rejects_a_malformed_request_naming_the_field(
        string queryString,
        string expectedField)
    {
        await using var store = new DuckDbStore(":memory:");
        var ct = TestContext.Current.CancellationToken;
        await IngestAsync(store, ct, Gauge("http.server.active", "/a", (T0, 1d)));

        var context = CreateContext(queryString);
        await (await CollectorEndpointExtensions.QueryMetricAsync(
            context, "http.server.active", store, ct)).ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Contains(expectedField, await ReadBodyAsync(context), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_query_route_truncates_at_the_series_limit_and_says_so()
    {
        await using var store = new DuckDbStore(":memory:");
        var ct = TestContext.Current.CancellationToken;
        await IngestAsync(store, ct,
            Gauge("http.server.active", "/a", (T0, 1d)),
            Gauge("http.server.active", "/b", (T0, 2d)),
            Gauge("http.server.active", "/c", (T0, 3d)));

        var context = CreateContext(
            $"start_time={T0Iso}&end_time={T0PlusMinuteIso}&group_by=http.route&series_limit=2");
        await (await CollectorEndpointExtensions.QueryMetricAsync(
            context, "http.server.active", store, ct)).ExecuteAsync(context);

        var result = JsonSerializer.Deserialize(
            await ReadBodyAsync(context), QylSerializerContext.Default.MetricQueryResult)!;
        Assert.Equal(2, result.Series.Count);
        Assert.True(result.Truncated);
    }

    private static async Task IngestAsync(DuckDbStore store, CancellationToken ct, params OtlpMetric[] metrics)
    {
        var scope = new ScopeMetrics();
        scope.Metrics.AddRange(metrics);
        var request = new ExportMetricsServiceRequest
        {
            ResourceMetrics =
            {
                new ResourceMetrics
                {
                    Resource = new Resource { Attributes = { Attr("service.name", "metric-api-tests") } },
                    ScopeMetrics = { scope }
                }
            }
        };

        var write = MetricStorageMapper.ToStorageRows(OtlpConverter.ConvertMetrics(request));
        await store.InsertMetricsAsync(write.Series, write.Points, ct);
    }

    private static OtlpMetric Gauge(string name, string route, params (ulong Time, double Value)[] points)
    {
        var gauge = new Gauge();
        foreach (var (time, value) in points)
        {
            gauge.DataPoints.Add(new NumberDataPoint
            {
                TimeUnixNano = time, AsDouble = value, Attributes = { Attr("http.route", route) }
            });
        }

        return new OtlpMetric { Name = name, Gauge = gauge };
    }

    private static OtlpMetric Counter(string name, params (ulong Time, double Value)[] points)
    {
        var sum = new Sum
        {
            IsMonotonic = true,
            AggregationTemporality = AggregationTemporality.Cumulative
        };
        foreach (var (time, value) in points)
            sum.DataPoints.Add(new NumberDataPoint { TimeUnixNano = time, AsDouble = value });
        return new OtlpMetric { Name = name, Unit = "{request}", Sum = sum };
    }

    private static OtlpMetric Histogram(string name, ulong time)
    {
        var point = new HistogramDataPoint { TimeUnixNano = time, Count = 4, Sum = 12 };
        point.ExplicitBounds.AddRange([1.0, 5.0]);
        point.BucketCounts.AddRange([1UL, 2UL, 1UL]);
        return new OtlpMetric
        {
            Name = name,
            Unit = "s",
            Histogram = new Histogram
            {
                AggregationTemporality = AggregationTemporality.Delta,
                DataPoints = { point }
            }
        };
    }

    // Attribute.value is an open `unknown`-shaped contract value, so a round-tripped
    // attribute carries a JsonElement rather than the string the collector wrote.
    private static string? AttributeString(Qyl.Api.Contracts.Common.Attribute attribute) =>
        attribute.Value switch
        {
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
            string value => value,
            _ => null
        };

    // MetricBucket.value is a `float64 | null` union, which the C# emitter lands on `object`;
    // over the wire it is a JSON number, so a round-tripped bucket carries a JsonElement.
    private static double BucketValue(Qyl.Api.Contracts.OTel.Metrics.MetricBucket bucket) =>
        bucket.Value switch
        {
            JsonElement element => element.GetDouble(),
            double value => value,
            _ => throw new InvalidOperationException($"Bucket carried no numeric value: {bucket.Value ?? "null"}")
        };

    private static KeyValue Attr(string key, string value) =>
        new() { Key = key, Value = new AnyValue { StringValue = value } };

    private static DefaultHttpContext CreateContext(string queryString = "")
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddLogging()
                .ConfigureHttpJsonOptions(static options =>
                    options.SerializerOptions.TypeInfoResolverChain.Insert(0, QylSerializerContext.Default))
                .BuildServiceProvider()
        };
        if (queryString.Length > 0)
            context.Request.QueryString = new QueryString("?" + queryString);
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> ReadBodyAsync(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
    }
}
