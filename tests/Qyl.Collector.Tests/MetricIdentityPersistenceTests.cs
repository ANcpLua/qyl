using System.Net;
using Google.Protobuf;
using Google.Protobuf.Collections;
using OpenTelemetry.Proto.Collector.Metrics.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Metrics.V1;
using OpenTelemetry.Proto.Resource.V1;
using Qyl.Collector.Mapping;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests;

[TestClass]
public sealed class MetricIdentityPersistenceTests(TestContext testContext)
{
    [TestMethod]
    [DataRow("resource")]
    [DataRow("scope")]
    [DataRow("point")]
    [DataRow("nested-point")]
    public async Task Withheld_attributes_keep_streams_distinct_after_reopen(string location)
    {
        var path = Path.Combine(Path.GetTempPath(), $"qyl-identity-{Guid.NewGuid():N}.duckdb");
        try
        {
            var request = new ExportMetricsServiceRequest();
            request.ResourceMetrics.Add(Resource(1, location));
            request.ResourceMetrics.Add(Resource(2, location));
            await using (var host = await CollectorApiTestHost.StartAsync(testContext.CancellationToken, path))
            {
                using var accepted = await host.SendAsync(HttpMethod.Post, "/v1/metrics", "ingest-reader", JsonFormatter.Default.Format(request));
                Assert.AreEqual(HttpStatusCode.OK, accepted.StatusCode);
                Assert.IsNull(JsonParser.Default.Parse<ExportMetricsServiceResponse>(await accepted.Content.ReadAsStringAsync(testContext.CancellationToken)).PartialSuccess);
                // Attribute order is not identity. The replay must not invent streams or observations.
                foreach (var resource in request.ResourceMetrics)
                {
                    Reverse(resource.Resource.Attributes);
                    foreach (var scope in resource.ScopeMetrics)
                    {
                        Reverse(scope.Scope.Attributes);
                        foreach (var point in scope.Metrics.SelectMany(static metric => metric.Gauge.DataPoints))
                        {
                            Reverse(point.Attributes);
                            foreach (var value in point.Attributes.Select(static attribute => attribute.Value))
                                if (value.ValueCase == AnyValue.ValueOneofCase.KvlistValue) Reverse(value.KvlistValue.Values);
                        }
                    }
                }
                using var replayed = await host.SendAsync(HttpMethod.Post, "/v1/metrics", "ingest-reader", JsonFormatter.Default.Format(request));
                Assert.AreEqual(HttpStatusCode.OK, replayed.StatusCode);
                Assert.IsNull(JsonParser.Default.Parse<ExportMetricsServiceResponse>(await replayed.Content.ReadAsStringAsync(testContext.CancellationToken)).PartialSuccess);
            }

            await using var reopened = await CollectorApiTestHost.StartAsync(testContext.CancellationToken, path);
            var series = await reopened.Store.ListMetricSeriesAsync("alpha", "identity.number", [], ct: testContext.CancellationToken);
            Assert.HasCount(2, series);
            var values = new List<long>();
            foreach (var stream in series)
            {
                var points = await reopened.Store.ListMetricPointsAsync(new MetricPointQuery
                {
                    ProjectId = "alpha", MetricName = stream.MetricName, SeriesId = stream.SeriesId, Limit = 10
                }, testContext.CancellationToken);
                var point = Assert.ContainsSingle(points);
                Assert.IsFalse(point.HasConflict);
                var data = Assert.IsInstanceOfType<Qyl.Api.Contracts.OTel.Metrics.MetricNumberPointData>(MetricMapper.ToContract(point, stream).Data);
                values.Add(Assert.IsInstanceOfType<Qyl.Api.Contracts.Common.AttributeIntValue>(data.Value).Value);
            }
            Assert.AreSequenceEqual([1L, 2L], values, SequenceOrder.InAnyOrder);

            using var catalogs = await reopened.SendAsync(HttpMethod.Get, "/api/v1/metrics?prefix=identity.number", "reader");
            Assert.AreEqual(HttpStatusCode.OK, catalogs.StatusCode);
            var catalog = await reopened.JsonAsync(catalogs);
            Assert.AreEqual(location is "resource" or "scope" ? 2 : 1, catalog.GetProperty("items").GetArrayLength());
            using var response = await reopened.SendAsync(HttpMethod.Get, "/api/v1/metrics/identity.number/series", "reader");
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            var json = await response.Content.ReadAsStringAsync(testContext.CancellationToken);
            Assert.DoesNotContain("private-instance-", json);
            Assert.DoesNotContain("service.instance.id", json);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".wal");
        }
    }

    private static ResourceMetrics Resource(int value, string location)
    {
        var identity = new KeyValue { Key = "service.instance.id", Value = new AnyValue { StringValue = $"private-instance-{value}" } };
        var point = new NumberDataPoint { TimeUnixNano = 1_790_000_000_000_000_000, AsInt = value };
        var scope = new ScopeMetrics { Scope = new InstrumentationScope { Name = "identity.scope" } };
        point.Attributes.Add(new KeyValue { Key = "identity.point", Value = new AnyValue { BoolValue = true } });
        scope.Scope.Attributes.Add(new KeyValue { Key = "identity.scope", Value = new AnyValue { BoolValue = true } });
        scope.Metrics.Add(new Metric { Name = "identity.number", Gauge = new Gauge { DataPoints = { point } } });
        var resource = new ResourceMetrics
        {
            Resource = new Resource { Attributes = { new KeyValue { Key = "service.name", Value = new AnyValue { StringValue = "identity-tests" } } } },
            ScopeMetrics = { scope }
        };
        switch (location)
        {
            case "resource": resource.Resource.Attributes.Add(identity); break;
            case "scope": scope.Scope.Attributes.Add(identity); break;
            case "point": point.Attributes.Add(identity); break;
            case "nested-point":
                point.Attributes.Add(new KeyValue { Key = "identity.map", Value = new AnyValue { KvlistValue = new KeyValueList
                { Values = { identity, new KeyValue { Key = "identity.visible", Value = new AnyValue { IntValue = 1 } } } } } });
                break;
        }
        return resource;
    }

    private static void Reverse(RepeatedField<KeyValue> attributes)
    {
        var reversed = attributes.Reverse().ToArray();
        attributes.Clear();
        attributes.Add(reversed);
    }
}
