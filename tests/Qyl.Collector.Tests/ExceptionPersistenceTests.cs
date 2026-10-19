using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Net;
using System.Text;
using System.Text.Json;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Grpc.Core;
using Grpc.Net.Client;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Metrics.V1;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Metrics.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Storage;
using WireSpan = OpenTelemetry.Proto.Trace.V1.Span;

namespace Qyl.Collector.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ExceptionPersistenceTests(TestContext testContext)
{
    private const ulong Time = 1_800_000_000_000_000_000;
    // Independent expectations from the documented persistence contract, not the
    // sanitizer's implementation constants; changing it must update this oracle.
    private const int InputLimit = 131_072;
    private const int PersistedByteLimit = 32_768;
    private const string FailureMarker = "[SANITIZATION_FAILED]";
    private const string HarmlessMessage = "The dependency rejected this state.\nRetry when it recovers.";
    private const string HarmlessStack = "System.InvalidOperationException: The dependency rejected this state.\r\n   at Checkout.Handler.Place() in /src/Checkout.cs:line 42\r\n   at Checkout.Api.Run()";
    private const string SensitiveText = """
        A dependency failed; diagnostic context follows.
        Server=db;User ID=synthetic-user;Password=synthetic-password with spaces&suffix;Database=orders;
        Server=db;Pwd='synthetic;quoted-password';Database=orders;
        Driver=db;PWD={synthetic;odbc-password};Database=orders;
        Authorization: Bearer synthetic-bearer-token
        Basic c3ludGhldGljOnN5bnRoZXRpYw==
        X-API-Key: synthetic-api-key;synthetic-api-key-suffix
        token=synthetic-assigned-token; continued
        eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJzeW50aGV0aWMifQ.synthetic-signature
        sk-proj-0123456789ABCDEF0123456789
        ghp_0123456789ABCDEF0123456789
        npm_0123456789ABCDEF0123456789
        xoxb-synthetic-test-credential-never-valid
        https://synthetic-user:synthetic-url-password@example.invalid/route?api_key=synthetic-query-secret#synthetic-fragment-secret
        https://example.invalid/route?opaque=synthetic-opaque-query
        https://example.invalid/route#synthetic-opaque-fragment
           at Checkout.Handler.Place() in /src/Checkout.cs:line 42
        End of diagnostic context.
        """;
    private const string RedactedText = """
        A dependency failed; diagnostic context follows.
        Server=db;User ID=[REDACTED];Password=[REDACTED];Database=orders;
        Server=db;Pwd='[REDACTED]';Database=orders;
        Driver=db;PWD={[REDACTED]};Database=orders;
        Authorization: Bearer [REDACTED]
        Basic [REDACTED]
        X-API-Key: [REDACTED]
        token=[REDACTED]; continued
        [REDACTED]
        [REDACTED]
        [REDACTED]
        [REDACTED]
        [REDACTED]
        https://example.invalid/route
        https://example.invalid/route
        https://example.invalid/route
           at Checkout.Handler.Place() in /src/Checkout.cs:line 42
        End of diagnostic context.
        """;
    private static readonly string[] DeniedKeys =
    [
        "authorization", "api_key", "password", "diagnostic.message", "exception.message.detail",
        "user.exception.stacktrace", "code.stacktrace", "baggage.secret",
        "exception.stacktrace.detail", "Exception.StackTrace", "EXCEPTION.MESSAGE"
    ];
    private CancellationToken Ct => testContext.CancellationToken;

    public static IEnumerable<object[]> Carriers()
    {
        // Every distinct keyed decode path, including recursive AnyValue maps, original
        // metric journals and each supported metric point shape. Discovery owns no resources.
        string[] carriers = ["span", "span_event", "span_link", "trace_resource", "trace_scope", "span_map", "span_array_map",
            "log", "log_resource", "log_scope", "log_map", "log_array_map", "metric_gauge", "metric_sum",
            "metric_histogram", "metric_exponential", "metric_summary", "metric_resource", "metric_scope",
            "metric_metadata", "metric_exemplar"];
        foreach (var carrier in carriers)
        {
            yield return [carrier, false];
            yield return [carrier, true];
        }
    }

    [TestMethod]
    [DynamicData(nameof(Carriers))]
    public async Task Exception_text_is_sanitized_before_persistence_and_survives_reopen_and_api(string carrier, bool grpc)
    {
        var failures = new ConcurrentQueue<(long Count, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Name is "qyl.collector.attributes.sanitization.failed") meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, count, tags, _) => failures.Enqueue((count, tags.ToArray())));
        listener.Start();
        var path = Path.Combine(Path.GetTempPath(), $"qyl-exceptions-{Guid.NewGuid():N}.duckdb");
        var cases = Cases();
        try
        {
            await using (var host = await CollectorApiTestHost.StartAsync(Ct, path))
            {
                foreach (var scenario in cases)
                {
                    await ExportAsync(host, carrier, grpc, scenario);
                    AssertStored(await StoredAsync(host.Store, carrier, scenario), scenario);
                }
            }

            // A new Collector host reopens the real file, rather than reading a cached
            // ingestion model. Its public API must return the same safe values.
            await using (var reopened = await CollectorApiTestHost.StartAsync(Ct, path))
            {
                foreach (var scenario in cases)
                {
                    AssertStored(await StoredAsync(reopened.Store, carrier, scenario), scenario);
                    var returned = await ApiAsync(reopened, carrier, scenario);
                    AssertValues(returned, scenario);
                }
            }

            Assert.AreEqual(4L, failures.Sum(static failure => failure.Count));
            var categories = new List<string>();
            var keys = new List<string>();
            foreach (var failure in failures)
            {
                Assert.HasCount(2, failure.Tags);
                keys.Add(Assert.IsInstanceOfType<string>(failure.Tags.Single(static tag => tag.Key is "qyl.attribute.key").Value));
                var category = Assert.IsInstanceOfType<string>(failure.Tags.Single(static tag => tag.Key is "error.type").Value);
                categories.Add(category);
                Assert.DoesNotContain("synthetic", string.Join(' ', failure.Tags.Select(static tag => tag.Value)), StringComparison.Ordinal);
            }
            Assert.AreSequenceEqual(["input_limit", "invalid_type", "invalid_url", "invalid_url"], categories.Order(StringComparer.Ordinal));
            Assert.AreSequenceEqual(["exception.message", "exception.message", "exception.message", "exception.stacktrace"], keys.Order(StringComparer.Ordinal));
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".wal");
        }
    }

    private sealed record Scenario(byte Id, AnyValue Message, string ExpectedMessage, string Stack = HarmlessStack, string? ExpectedStack = null);

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Structured_body_budget_preserves_unicode_but_still_counts_nontext_nodes(bool grpc)
    {
        var text = string.Concat(Enumerable.Repeat("😀", 15_000));
        var path = Path.Combine(Path.GetTempPath(), $"qyl-log-body-budget-{Guid.NewGuid():N}.duckdb");
        var request = new ExportLogsServiceRequest { ResourceLogs = { new ResourceLogs { ScopeLogs = { new ScopeLogs
        {
            LogRecords =
            {
                new LogRecord { TimeUnixNano = Time, EventName = "unicode", Body = new AnyValue
                    { KvlistValue = new KeyValueList { Values = { Attribute("details", Text(text)) } } } },
                new LogRecord { TimeUnixNano = Time, EventName = "nodes", Body = new AnyValue
                    { ArrayValue = new ArrayValue { Values = { Enumerable.Range(0, 15_000).Select(static _ => new AnyValue()) } } } }
            }
        } } } } };
        try
        {
            await using (var host = await CollectorApiTestHost.StartAsync(Ct, path))
            {
                var ack = await SendAsync(host, grpc, "/v1/logs", LogsServiceReflection.Descriptor.Services[0], request, ExportLogsServiceResponse.Parser);
                Assert.AreEqual(0L, ack.PartialSuccess?.RejectedLogRecords ?? 0);
            }
            await using var reopened = await CollectorApiTestHost.StartAsync(Ct, path);
            var stored = await reopened.Store.GetLogsAsync("alpha", ct: Ct);
            Assert.HasCount(2, stored);
            var unicode = stored.Single(static log => log.EventName is "unicode").Body!;
            Assert.IsGreaterThan(65_536, Encoding.UTF8.GetByteCount(unicode), "Escaping expands storage bytes, not the decoded body budget.");
            using var body = JsonDocument.Parse(unicode);
            Assert.AreEqual(text, Assert.ContainsSingle(Find(body.RootElement, "details")).GetString());
            using var response = await reopened.SendAsync(HttpMethod.Get, "/api/v1/logs", "reader");
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            var page = await reopened.JsonAsync(response);
            var items = page.GetProperty("items").EnumerateArray().ToArray();
            Assert.AreEqual(text, Assert.ContainsSingle(Find(items.Single(static log => log.GetProperty("event_name").GetString() is "unicode").GetProperty("body"), "details")).GetString());
            var nodes = items.Single(static log => log.GetProperty("event_name").GetString() is "nodes").GetProperty("body").GetString()!;
            Assert.EndsWith("...[truncated]", nodes, StringComparison.Ordinal);
            Assert.IsLessThanOrEqualTo(65_536, Encoding.UTF8.GetByteCount(nodes));
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".wal");
        }
    }

    private static Scenario[] Cases()
    {
        var shrink = "Before\nPassword=" + new string('p', 60_000) + ";\nAfter";
        var longText = string.Concat(Enumerable.Repeat("😀", 10_000)) + "\nPassword=synthetic-truncated-password;";
        // UTF-8 truncation leaves 8191 complete four-byte runes, plus the three-byte marker.
        var bounded = string.Concat(Enumerable.Repeat("😀", 8191)) + "…";
        return
        [
            new(1, Text(HarmlessMessage), HarmlessMessage),
            new(2, Text(SensitiveText), RedactedText, SensitiveText, RedactedText),
            new(3, Text("Unparseable https://[invalid-host/route?secret=synthetic-forced-failure"), FailureMarker),
            new(4, Text(shrink), "Before\nPassword=[REDACTED];\nAfter"),
            new(5, Text(longText), bounded),
            new(6, Text(new string('x', InputLimit + 1)), FailureMarker),
            new(7, new AnyValue { IntValue = 42 }, FailureMarker),
            new(8, Text(HarmlessMessage), HarmlessMessage,
                "Unparseable https://[invalid-host/route?secret=synthetic-stack-failure", FailureMarker)
        ];
    }

    private static AnyValue Text(string value) => new() { StringValue = value };
    private static KeyValue Attribute(string key, AnyValue value) => new() { Key = key, Value = value };
    private static string Name(Scenario scenario) => "exception-case-" + scenario.Id;
    private static ByteString Id(byte value, int length) => ByteString.CopyFrom(Enumerable.Repeat(value, length).ToArray());

    private static KeyValue[] Attributes(Scenario scenario) =>
    [
        Attribute("exception.message", scenario.Message), Attribute("exception.stacktrace", Text(scenario.Stack)),
        Attribute("checkout.accepted", new AnyValue { BoolValue = false }), Attribute("checkout.stage", Text("persisted")),
        .. DeniedKeys.Select(static key => Attribute(key, Text("synthetic-denied-credential")))
    ];

    private static AnyValue Map(Scenario scenario) => new() { KvlistValue = new KeyValueList { Values = { Attributes(scenario) } } };
    private static AnyValue ArrayMap(Scenario scenario) => new() { ArrayValue = new ArrayValue { Values = { Map(scenario) } } };

    private async Task ExportAsync(CollectorApiTestHost host, string carrier, bool grpc, Scenario scenario)
    {
        var resource = new Resource { Attributes = { Attribute("service.name", Text(Name(scenario))) } };
        var scope = new InstrumentationScope { Name = "ExceptionPersistence" };
        if (carrier.EndsWith("_resource", StringComparison.Ordinal)) resource.Attributes.Add(Attributes(scenario));
        if (carrier.EndsWith("_scope", StringComparison.Ordinal)) scope.Attributes.Add(Attributes(scenario));
        if (carrier.StartsWith("log", StringComparison.Ordinal))
        {
            var log = new LogRecord { TimeUnixNano = Time + scenario.Id, EventName = Name(scenario), Body = Text(Name(scenario)) };
            if (carrier is "log") log.Attributes.Add(Attributes(scenario));
            if (carrier is "log_map") log.Body = Map(scenario);
            if (carrier is "log_array_map") log.Body = ArrayMap(scenario);
            var request = new ExportLogsServiceRequest { ResourceLogs = { new ResourceLogs
                { Resource = resource, ScopeLogs = { new ScopeLogs { Scope = scope, LogRecords = { log } } } } } };
            var response = await SendAsync(host, grpc, "/v1/logs", LogsServiceReflection.Descriptor.Services[0], request, ExportLogsServiceResponse.Parser);
            Assert.AreEqual(0L, response.PartialSuccess?.RejectedLogRecords ?? 0);
        }
        else if (carrier.StartsWith("metric", StringComparison.Ordinal))
        {
            var metric = Metric(scenario, carrier);
            var request = new ExportMetricsServiceRequest { ResourceMetrics = { new ResourceMetrics
                { Resource = resource, ScopeMetrics = { new ScopeMetrics { Scope = scope, Metrics = { metric } } } } } };
            var response = await SendAsync(host, grpc, "/v1/metrics", MetricsServiceReflection.Descriptor.Services[0], request, ExportMetricsServiceResponse.Parser);
            Assert.AreEqual(0L, response.PartialSuccess?.RejectedDataPoints ?? 0);
        }
        else
        {
            var span = new WireSpan { TraceId = Id(scenario.Id, 16), SpanId = Id(scenario.Id, 8), Name = Name(scenario),
                StartTimeUnixNano = Time, EndTimeUnixNano = Time + 1 };
            if (carrier is "span") span.Attributes.Add(Attributes(scenario));
            if (carrier is "span_map") span.Attributes.Add(Attribute("details", Map(scenario)));
            if (carrier is "span_array_map") span.Attributes.Add(Attribute("details", ArrayMap(scenario)));
            if (carrier is "span_event") span.Events.Add(new WireSpan.Types.Event
                { Name = "exception", TimeUnixNano = Time, Attributes = { Attributes(scenario) } });
            if (carrier is "span_link") span.Links.Add(new WireSpan.Types.Link
                { TraceId = Id(100, 16), SpanId = Id(100, 8), Attributes = { Attributes(scenario) } });
            var request = new ExportTraceServiceRequest { ResourceSpans = { new ResourceSpans
                { Resource = resource, ScopeSpans = { new ScopeSpans { Scope = scope, Spans = { span } } } } } };
            var response = await SendAsync(host, grpc, "/v1/traces", TraceServiceReflection.Descriptor.Services[0], request, ExportTraceServiceResponse.Parser);
            Assert.AreEqual(0L, response.PartialSuccess?.RejectedSpans ?? 0);
        }
    }

    private static Metric Metric(Scenario scenario, string carrier)
    {
        var metric = new Metric { Name = Name(scenario) };
        if (carrier is "metric_metadata") metric.Metadata.Add(Attributes(scenario));
        var attributes = carrier is "metric_gauge" or "metric_sum" or "metric_histogram" or "metric_exponential" or "metric_summary"
            ? Attributes(scenario) : [];
        var number = new NumberDataPoint { TimeUnixNano = Time, AsInt = 1, Attributes = { attributes } };
        if (carrier is "metric_exemplar") number.Exemplars.Add(new Exemplar
            { TimeUnixNano = Time, AsInt = 1, FilteredAttributes = { Attributes(scenario) } });
        switch (carrier)
        {
            case "metric_sum": metric.Sum = new Sum { AggregationTemporality = AggregationTemporality.Cumulative,
                IsMonotonic = true, DataPoints = { number } }; break;
            case "metric_histogram": metric.Histogram = new Histogram { AggregationTemporality = AggregationTemporality.Cumulative,
                DataPoints = { new HistogramDataPoint { TimeUnixNano = Time, Count = 1, Sum = 1, BucketCounts = { 1 }, Attributes = { attributes } } } }; break;
            case "metric_exponential": metric.ExponentialHistogram = new ExponentialHistogram { AggregationTemporality = AggregationTemporality.Cumulative,
                DataPoints = { new ExponentialHistogramDataPoint { TimeUnixNano = Time, Count = 1, Sum = 1,
                    Positive = new ExponentialHistogramDataPoint.Types.Buckets { BucketCounts = { 1 } }, Attributes = { attributes } } } }; break;
            case "metric_summary": metric.Summary = new Summary { DataPoints = { new SummaryDataPoint
                { TimeUnixNano = Time, Count = 1, Sum = 1, Attributes = { attributes } } } }; break;
            default: metric.Gauge = new Gauge { DataPoints = { number } }; break;
        }
        return metric;
    }

    private async Task<TResponse> SendAsync<TRequest, TResponse>(CollectorApiTestHost host, bool grpc, string path,
        ServiceDescriptor service, TRequest request, MessageParser<TResponse> responseParser)
        where TRequest : class, IMessage<TRequest> where TResponse : class, IMessage<TResponse>
    {
        if (!grpc)
        {
            using var response = await host.SendProtobufAsync(path, request, "ingester");
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            return responseParser.ParseFrom(await response.Content.ReadAsByteArrayAsync(Ct));
        }
        using var channel = GrpcChannel.ForAddress(host.GrpcAddress);
        var method = new Method<TRequest, TResponse>(MethodType.Unary, service.FullName, service.Methods[0].Name,
            Marshallers.Create<TRequest>(static value => value.ToByteArray(), bytes => { var parsed = (TRequest)request.Descriptor.Parser.ParseFrom(bytes); return parsed; }),
            Marshallers.Create<TResponse>(static value => value.ToByteArray(), responseParser.ParseFrom));
        using var call = channel.CreateCallInvoker().AsyncUnaryCall(method, null,
            new CallOptions(new Metadata { { OtlpConstants.ApiKeyHeaderName, "ingester" } }, cancellationToken: Ct), request);
        return await call.ResponseAsync;
    }

    private async Task<string> StoredAsync(DuckDbStore store, string carrier, Scenario scenario)
    {
        if (carrier.StartsWith("log", StringComparison.Ordinal))
        {
            var log = (await store.GetLogsAsync("alpha", ct: Ct)).Single(log => log.EventName == Name(scenario));
            return (carrier switch { "log_resource" => log.ResourceJson, "log_scope" => log.ScopeAttributesJson,
                "log_map" or "log_array_map" => log.Body, _ => log.AttributesJson })!;
        }
        if (carrier.StartsWith("metric", StringComparison.Ordinal))
        {
            var series = Assert.ContainsSingle(await store.ListMetricSeriesAsync("alpha", Name(scenario), [], ct: Ct));
            if (carrier is "metric_resource") return series.ResourceJson!;
            if (carrier is "metric_scope") return series.ScopeAttributesJson!;
            var point = Assert.ContainsSingle(await store.ListMetricPointsAsync(new MetricPointQuery
                { ProjectId = "alpha", MetricName = Name(scenario), SeriesId = series.SeriesId, Limit = 10 }, Ct)).Observation;
            return (carrier switch { "metric_metadata" => point.InstrumentMetadataJson,
                "metric_exemplar" => point.ExemplarsJson, _ => series.AttributesJson })!;
        }
        var span = Assert.ContainsSingle(await store.GetTraceAsync(Convert.ToHexStringLower(Id(scenario.Id, 16).Span), "alpha", Ct));
        return (carrier switch { "trace_resource" => span.ResourceJson, "trace_scope" => span.ScopeAttributesJson,
            "span_event" => Assert.ContainsSingle(SpanChildStorage.DeserializeEvents(span.EventsJson)!).AttributesJson,
            "span_link" => Assert.ContainsSingle(SpanChildStorage.DeserializeLinks(span.LinksJson)!).AttributesJson,
            _ => span.AttributesJson })!;
    }

    private async Task<JsonElement> ApiAsync(CollectorApiTestHost host, string carrier, Scenario scenario)
    {
        var path = "/api/v1/traces/" + Convert.ToHexStringLower(Id(scenario.Id, 16).Span) + "/spans";
        if (carrier.StartsWith("log", StringComparison.Ordinal)) path = "/api/v1/logs";
        if (carrier.StartsWith("metric", StringComparison.Ordinal))
        {
            path = "/api/v1/metrics/" + Name(scenario) + "/series";
            if (carrier is not ("metric_scope" or "metric_resource"))
            {
                var series = Assert.ContainsSingle(await host.Store.ListMetricSeriesAsync("alpha", Name(scenario), [], ct: Ct));
                path += "/" + series.SeriesId + "/points";
            }
        }
        using var response = await host.SendAsync(HttpMethod.Get, path, "reader");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var page = await host.JsonAsync(response);
        var items = page.GetProperty("items").EnumerateArray();
        return carrier.StartsWith("log", StringComparison.Ordinal)
            ? items.Single(item => item.GetProperty("event_name").GetString() == Name(scenario))
            : Assert.ContainsSingle(items);
    }

    private static void AssertStored(string json, Scenario scenario)
    {
        using var document = JsonDocument.Parse(json);
        AssertValues(document.RootElement, scenario);
    }

    private static void AssertValues(JsonElement json, Scenario scenario)
    {
        var message = Assert.ContainsSingle(Find(json, "exception.message"));
        Assert.AreEqual(scenario.ExpectedMessage, message.GetString());
        var expectedStack = scenario.ExpectedStack ?? scenario.Stack;
        Assert.AreEqual(expectedStack, Assert.ContainsSingle(Find(json, "exception.stacktrace")).GetString());
        Assert.IsFalse(Assert.ContainsSingle(Find(json, "checkout.accepted")).GetBoolean());
        Assert.AreEqual("persisted", Assert.ContainsSingle(Find(json, "checkout.stage")).GetString());
        foreach (var key in DeniedKeys) Assert.IsEmpty(Find(json, key), key);
        Assert.DoesNotContain("synthetic-", json.ToString(), StringComparison.Ordinal);
        Assert.IsLessThanOrEqualTo(PersistedByteLimit, Encoding.UTF8.GetByteCount(message.GetString()!));
    }

    private static IEnumerable<JsonElement> Find(JsonElement node, string key)
    {
        if (node.ValueKind is JsonValueKind.Array)
            return node.EnumerateArray().SelectMany(item => Find(item, key));
        if (node.ValueKind is not JsonValueKind.Object) return [];
        if (node.TryGetProperty("key", out var name) && name.ValueKind is JsonValueKind.String && name.GetString() == key)
            return [node.GetProperty("value")];
        return node.EnumerateObject().SelectMany(property => property.Name == key
            ? new[] { property.Value } : Find(property.Value, key));
    }
}
