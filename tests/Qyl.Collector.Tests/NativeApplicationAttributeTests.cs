using System.Net;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Text.Json;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Metrics.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Metrics.V1;
using OpenTelemetry.Proto.Resource.V1;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Storage;
using WireSpan = OpenTelemetry.Proto.Trace.V1.Span;
using static Qyl.Collector.Tests.OtlpTestSpans;

namespace Qyl.Collector.Tests;

[TestClass]
public sealed class NativeApplicationAttributeTests(TestContext testContext)
{
    // Attribute names are not a registry allowlist, and values are typed AnyValues.
    // https://github.com/open-telemetry/opentelemetry-specification/blob/v1.61.0/specification/common/README.md#attribute
    [TestMethod]
    [DataRow("span", "checkout.")]
    [DataRow("event", "checkout.")]
    [DataRow("link", "checkout.")]
    [DataRow("resource", "checkout.")]
    [DataRow("scope", "checkout.")]
    [DataRow("log", "checkout.")]
    [DataRow("span", "qyl.app.")]
    [DataRow("event", "qyl.app.")]
    [DataRow("link", "qyl.app.")]
    [DataRow("resource", "qyl.app.")]
    [DataRow("scope", "qyl.app.")]
    [DataRow("log", "qyl.app.")]
    public async Task Attributes_keep_their_original_names_types_and_values(string carrier, string prefix)
    {
        KeyValue[] values =
        [
            Int(prefix + "sequence", 9_007_199_254_740_993L),
            Bool(prefix + "accepted", false),
            Double(prefix + "amount", 1.25),
            String(prefix + "label", new string('x', 300)),
            String(prefix + "empty", string.Empty),
            new() { Key = prefix + "labels", Value = new AnyValue { ArrayValue = new ArrayValue
                { Values = { new AnyValue { StringValue = "first" }, new AnyValue { StringValue = "second" } } } } },
            .. Enumerable.Range(1, 17).Select(i => String($"{prefix}field{i}", "v"))
        ];
        await using var host = await CollectorApiTestHost.StartAsync(testContext.CancellationToken);
        string? json;
        if (carrier is "log")
        {
            var log = new LogRecord { TimeUnixNano = 1_800_000_000_000_000_000,
                Body = new AnyValue { StringValue = "application event" } };
            log.Attributes.Add(values);
            var request = new ExportLogsServiceRequest { ResourceLogs = { new ResourceLogs
                { Resource = new Resource { Attributes = { String("service.name", "application") } },
                    ScopeLogs = { new ScopeLogs { Scope = new InstrumentationScope { Name = "Application" }, LogRecords = { log } } } } } };
            using var response = await host.SendProtobufAsync("/v1/logs", request, "ingester");
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            var ack = ExportLogsServiceResponse.Parser.ParseFrom(await response.Content.ReadAsByteArrayAsync(testContext.CancellationToken));
            Assert.AreEqual(0L, ack.PartialSuccess?.RejectedLogRecords ?? 0);
            json = Assert.ContainsSingle(await host.Store.GetLogsAsync("alpha", ct: testContext.CancellationToken)).AttributesJson;
        }
        else
        {
            var request = Request("Application", carrier is "span" ? values : []);
            var resource = request.ResourceSpans[0];
            var scope = resource.ScopeSpans[0];
            var span = scope.Spans[0];
            if (carrier is "resource") resource.Resource.Attributes.Add(values);
            if (carrier is "scope") scope.Scope.Attributes.Add(values);
            if (carrier is "event")
            {
                var e = new WireSpan.Types.Event { Name = "checkout.ready", TimeUnixNano = span.StartTimeUnixNano };
                e.Attributes.Add(values);
                span.Events.Add(e);
            }
            if (carrier is "link")
            {
                var link = new WireSpan.Types.Link { TraceId = span.TraceId, SpanId = span.SpanId };
                link.Attributes.Add(values);
                span.Links.Add(link);
            }
            await host.IngestTracesAsync(request);
            var stored = Assert.ContainsSingle(await host.Store.GetSpansAsync("alpha", ct: testContext.CancellationToken));
            json = carrier switch
            {
                "resource" => stored.ResourceJson,
                "scope" => stored.ScopeAttributesJson,
                "event" => Assert.ContainsSingle(SpanChildStorage.DeserializeEvents(stored.EventsJson)!).AttributesJson,
                "link" => Assert.ContainsSingle(SpanChildStorage.DeserializeLinks(stored.LinksJson)!).AttributesJson,
                _ => stored.AttributesJson
            };
        }

        using var document = JsonDocument.Parse(json ?? "{}");
        var attributes = document.RootElement;
        AssertValue(attributes, prefix + "sequence", OtlpAttributeValue.FromInt(9_007_199_254_740_993L));
        AssertValue(attributes, prefix + "accepted", OtlpAttributeValue.FromBool(false));
        AssertValue(attributes, prefix + "amount", OtlpAttributeValue.FromDouble(1.25));
        Assert.AreEqual(new string('x', 300), attributes.GetProperty(prefix + "label").GetString());
        Assert.AreEqual(string.Empty, attributes.GetProperty(prefix + "empty").GetString());
        Assert.AreSequenceEqual(["first", "second"], attributes.GetProperty(prefix + "labels").EnumerateArray().Select(static e => e.GetString()));
        Assert.AreEqual("v", attributes.GetProperty(prefix + "field17").GetString());
    }

    [TestMethod]
    public async Task Custom_metric_attributes_remain_distinct_after_database_reopen()
    {
        var request = new ExportMetricsServiceRequest { ResourceMetrics = { new ResourceMetrics
        {
            Resource = new Resource { Attributes = { String("service.name", "application"), String("checkout.tenant", "acme") } },
            ScopeMetrics = { new ScopeMetrics { Scope = new InstrumentationScope { Name = "Application" }, Metrics =
            { new Metric { Name = "checkout.depth", Gauge = new Gauge { DataPoints =
            {
                new NumberDataPoint { TimeUnixNano = 1_800_000_000_000_000_000, AsInt = 1, Attributes = { String("checkout.region", "north") } },
                new NumberDataPoint { TimeUnixNano = 1_800_000_000_000_000_000, AsInt = 2, Attributes = { String("checkout.region", "south") } }
            } } } } } }
        } } };
        var path = Path.Combine(Path.GetTempPath(), $"qyl-application-attributes-{Guid.NewGuid():N}.duckdb");
        try
        {
            await using (var store = new DuckDbStore(path))
            {
                var batch = OtlpConverter.ConvertMetrics(request);
                Assert.AreEqual(0L, batch.RejectedDataPoints);
                var rows = MetricStorageMapper.ToStorageRows(batch, "alpha");
                await store.InsertMetricsAsync(rows.Series, rows.Observations, testContext.CancellationToken);
            }

            await using var reopened = new DuckDbStore(path);
            var series = await reopened.ListMetricSeriesAsync("alpha", "checkout.depth", [], ct: testContext.CancellationToken);
            Assert.HasCount(2, series);
            Assert.AreEqual(2, series.Select(static s => s.SeriesId).Distinct().Count());
            Assert.AreSequenceEqual(["north", "south"], series.Select(static s =>
                ReadString(s.AttributesJson, "checkout.region")).Order(StringComparer.Ordinal));
            foreach (var s in series)
                Assert.AreEqual("acme", ReadString(s.ResourceJson, "checkout.tenant"));
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".wal");
        }
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task Identical_log_state_and_scope_values_keep_correlation_while_conflicts_remain_rejections()
    {
        var measurements = new ConcurrentQueue<(long Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Name is "qyl.collector.attributes.coalesced")
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) => measurements.Enqueue((value, tags.ToArray())));
        listener.Start();
        var span = Request("Application", []).ResourceSpans[0].ScopeSpans[0].Spans[0];
        var traceId = Convert.ToHexStringLower(span.TraceId.Span);
        var retained = new LogRecord
        {
            TimeUnixNano = span.StartTimeUnixNano,
            TraceId = span.TraceId, SpanId = span.SpanId, EventName = "WorkCompleted",
            Body = new AnyValue { StringValue = "completed" },
            Attributes = { String("TraceId", traceId), String("TraceId", traceId), Bool("checkout.accepted", false) }
        };
        var conflicting = new LogRecord
        {
            TimeUnixNano = span.StartTimeUnixNano,
            Body = new AnyValue { StringValue = "conflicting values" },
            Attributes = { Int("checkout.sequence", 42), String("checkout.sequence", "42") }
        };
        var request = new ExportLogsServiceRequest { ResourceLogs = { new ResourceLogs
        {
            Resource = new Resource { Attributes = { String("service.name", "application"),
                String("checkout.tenant", "acme"), String("checkout.tenant", "acme") } },
            ScopeLogs = { new ScopeLogs { Scope = new InstrumentationScope { Name = "Application",
                Attributes = { Bool("scope.enabled", false), Bool("scope.enabled", false) } },
                LogRecords = { retained, conflicting, new LogRecord { TimeUnixNano = span.StartTimeUnixNano,
                    Body = new AnyValue { StringValue = "valid sibling" } } } } }
        } } };
        await using var host = await CollectorApiTestHost.StartAsync(testContext.CancellationToken);
        using var response = await host.SendProtobufAsync("/v1/logs", request, "ingester");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var ack = ExportLogsServiceResponse.Parser.ParseFrom(await response.Content.ReadAsByteArrayAsync(testContext.CancellationToken));
        Assert.AreEqual(1L, ack.PartialSuccess.RejectedLogRecords);
        Assert.Contains("conflict", ack.PartialSuccess.ErrorMessage, StringComparison.Ordinal);
        var saved = await host.Store.GetLogsAsync("alpha", ct: testContext.CancellationToken);
        Assert.HasCount(2, saved);
        var log = Assert.ContainsSingle(static l => l.EventName is "WorkCompleted", saved);
        Assert.AreEqual(traceId, log.TraceId);
        Assert.AreEqual(Convert.ToHexStringLower(span.SpanId.Span), log.SpanId);
        using var attributes = JsonDocument.Parse(log.AttributesJson!);
        Assert.AreEqual(traceId, attributes.RootElement.GetProperty("TraceId").GetString());
        Assert.AreEqual(1, attributes.RootElement.EnumerateObject().Count(static a => a.Name is "TraceId"));
        Assert.IsFalse(attributes.RootElement.GetProperty("checkout.accepted").GetBoolean());
        Assert.AreEqual(3L, measurements.Sum(static m => m.Value), "Resource, scope and log duplicates must be visible diagnostics.");
        foreach (var measurement in measurements)
        {
            var tag = Assert.ContainsSingle(measurement.Tags);
            Assert.AreEqual("qyl.attribute.namespace", tag.Key);
            Assert.IsTrue(tag.Value is string { Length: > 0 }, "Coalescing diagnostics must name the attribute namespace.");
        }
    }

    private static string? ReadString(string? json, string key)
    {
        using var document = JsonDocument.Parse(json ?? "{}");
        return document.RootElement.GetProperty(key).GetString();
    }

    private static void AssertValue(JsonElement attributes, string key, OtlpAttributeValue value) =>
        Assert.AreEqual(JsonSerializer.Serialize(StoredRecordMapper.ToStored(value), StorageJsonSerializerContext.Default.StoredAttributeValue),
            attributes.GetProperty(key).GetRawText());
}
