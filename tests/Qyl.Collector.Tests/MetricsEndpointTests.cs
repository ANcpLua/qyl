using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Proto.Collector.Metrics.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Metrics.V1;
using OpenTelemetry.Proto.Resource.V1;
using Qyl.Api.Contracts.Common.Pagination;
using Qyl.Api.Contracts.OTel.Metrics;
using Qyl.Collector.Hosting;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Storage;
using ProtoAggregationTemporality = OpenTelemetry.Proto.Metrics.V1.AggregationTemporality;

namespace Qyl.Collector.Tests;

public sealed class MetricsEndpointTests
{
    private const ulong GaugeTime = 2_000_000_000;
    private const ulong SumTime = 3_000_000_000;

    [Fact]
    public async Task Generated_metric_contract_pages_use_a_stable_cursor_and_string_encoded_integers()
    {
        await using var store = await BuildStoreAsync();
        var values = new HashSet<long>();
        string? cursor = null;
        var pageCount = 0;

        do
        {
            var query = "?type=gauge&limit=1" +
                        (cursor is null ? "" : "&cursor=" + Uri.EscapeDataString(cursor));
            var context = CreateContext(query);
            var result = await CollectorEndpointExtensions.GetMetricsAsync(
                context,
                store,
                TestContext.Current.CancellationToken);
            await result.ExecuteAsync(context);

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
            var json = await ReadBodyAsync(context);
            Assert.Contains($"\"time_unix_nano\":\"{GaugeTime}\"", json);
            Assert.Contains("\"as_int\":\"", json);
            var page = JsonSerializer.Deserialize(json, QylSerializerContext.Default.CursorPageMetricPoint);
            var gauge = Assert.IsType<GaugeMetricPoint>(Assert.Single(Assert.IsType<CursorPageMetricPoint>(page).Items));
            Assert.True(values.Add(Assert.IsType<MetricIntegerValue>(gauge.Value).AsInt));

            cursor = page.NextCursor;
            Assert.Equal(cursor is not null, page.HasMore);
            pageCount++;
        } while (cursor is not null);

        Assert.Equal(3, pageCount);
        Assert.Equal(3, values.Count);
    }

    [Fact]
    public async Task Metrics_query_filters_match_the_schema_and_legacy_rows_stay_private()
    {
        await using var store = await BuildStoreAsync();
        await store.InsertMetricsAsync(
        [
            new MetricStorageRow
            {
                ProjectId = "default",
                MetricId = "metric_00000000000000000000000000000000",
                MetricName = "legacy.gauge",
                MetricType = MetricStorageTypes.Gauge,
                TimeUnixNano = SumTime,
                Value = 42,
                ServiceName = "legacy-service"
            }
        ], TestContext.Current.CancellationToken);

        var filter = QueryString.Create(
        [
            new KeyValuePair<string, string?>("name", "sum.metric"),
            new KeyValuePair<string, string?>("type", "sum"),
            new KeyValuePair<string, string?>("serviceName", "service-b"),
            new KeyValuePair<string, string?>("startTime", DateTimeOffset.FromUnixTimeSeconds(3).ToString("O")),
            new KeyValuePair<string, string?>("endTime", DateTimeOffset.FromUnixTimeSeconds(3).ToString("O")),
            new KeyValuePair<string, string?>("limit", "10")
        ]).ToString();
        var context = CreateContext(filter);
        var result = await CollectorEndpointExtensions.GetMetricsAsync(
            context,
            store,
            TestContext.Current.CancellationToken);
        await result.ExecuteAsync(context);

        var page = JsonSerializer.Deserialize(
            await ReadBodyAsync(context),
            QylSerializerContext.Default.CursorPageMetricPoint);
        var sum = Assert.IsType<SumMetricPoint>(Assert.Single(Assert.IsType<CursorPageMetricPoint>(page).Items));
        Assert.Equal("sum.metric", sum.Name);
        Assert.Equal(2.5, Assert.IsType<MetricDoubleValue>(sum.Value).AsDouble);
        Assert.False(page.HasMore);
        Assert.Null(page.NextCursor);

        var legacyContext = CreateContext("?name=legacy.gauge");
        var legacyResult = await CollectorEndpointExtensions.GetMetricsAsync(
            legacyContext,
            store,
            TestContext.Current.CancellationToken);
        await legacyResult.ExecuteAsync(legacyContext);
        var legacyPage = JsonSerializer.Deserialize(
            await ReadBodyAsync(legacyContext),
            QylSerializerContext.Default.CursorPageMetricPoint);
        Assert.Empty(Assert.IsType<CursorPageMetricPoint>(legacyPage).Items);
    }

    private static async Task<DuckDbStore> BuildStoreAsync()
    {
        var gaugeMetric = new Metric { Name = "gauge.metric", Gauge = new Gauge() };
        for (var index = 0; index < 3; index++)
        {
            gaugeMetric.Gauge.DataPoints.Add(new NumberDataPoint
            {
                StartTimeUnixNano = 0,
                TimeUnixNano = GaugeTime,
                AsInt = long.MaxValue - index,
                Attributes =
                {
                    new KeyValue
                    {
                        Key = "http.request.method",
                        Value = new AnyValue { StringValue = $"METHOD-{index}" }
                    }
                }
            });
        }

        var request = new ExportMetricsServiceRequest
        {
            ResourceMetrics =
            {
                ResourceWithMetrics("service-a", gaugeMetric),
                ResourceWithMetrics("service-b", new Metric
                {
                    Name = "sum.metric",
                    Sum = new Sum
                    {
                        AggregationTemporality = ProtoAggregationTemporality.Delta,
                        IsMonotonic = false,
                        DataPoints =
                        {
                            new NumberDataPoint
                            {
                                StartTimeUnixNano = 1_000_000_000,
                                TimeUnixNano = SumTime,
                                AsDouble = 2.5
                            }
                        }
                    }
                })
            }
        };

        var store = new DuckDbStore(":memory:");
        var rows = IngestionStorageMapper.ToMetricStorageRows(OtlpConverter.ConvertMetrics(request));
        await store.InsertMetricsAsync(rows, TestContext.Current.CancellationToken);
        return store;
    }

    private static ResourceMetrics ResourceWithMetrics(string serviceName, Metric metric) =>
        new()
        {
            Resource = new Resource
            {
                Attributes =
                {
                    new KeyValue
                    {
                        Key = "service.name",
                        Value = new AnyValue { StringValue = serviceName }
                    }
                }
            },
            ScopeMetrics = { new ScopeMetrics { Metrics = { metric } } }
        };

    private static DefaultHttpContext CreateContext(string query)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider()
        };
        context.Request.QueryString = new QueryString(query);
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
