using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Resource.V1;
using Qyl.Api.Contracts.Common.Pagination;
using Qyl.Api.Contracts.Streaming;
using Qyl.Collector.Hosting;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests;

[TestClass]
public sealed class LogEventContractTests(TestContext testContext)
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Otlp_event_name_and_body_survive_storage_rest_and_sse(bool canonicalBrowserEvent)
    {
        var eventName = canonicalBrowserEvent ? "browser.web_vital" : "mcp.request";
        var routeKey = canonicalBrowserEvent ? "url.path" : "page.route";
        var valueKey = canonicalBrowserEvent ? "browser.web_vital.value" : "web.vital.value";
        var logBody = canonicalBrowserEvent ? "LCP measured" : "MCP JSON-RPC request completed";
        const string TraceId = "11111111111111111111111111111111";
        const string SpanId = "2222222222222222";
        var request = new ExportLogsServiceRequest
        {
            ResourceLogs =
            {
                new ResourceLogs
                {
                    Resource = new Resource
                    {
                        Attributes =
                        {
                            new KeyValue
                            {
                                Key = "service.name",
                                Value = new AnyValue { StringValue = "evaluation-service" }
                            }
                        }
                    },
                    ScopeLogs =
                    {
                        new ScopeLogs
                        {
                            LogRecords =
                            {
                                new LogRecord
                                {
                                    TimeUnixNano = 2_000_000_000,
                                    ObservedTimeUnixNano = 2_100_000_000,
                                    SeverityNumber = SeverityNumber.Info,
                                    SeverityText = "INFO",
                                    EventName = eventName,
                                    TraceId = Google.Protobuf.ByteString.CopyFrom(Convert.FromHexString(TraceId)),
                                    SpanId = Google.Protobuf.ByteString.CopyFrom(Convert.FromHexString(SpanId)),
                                    Body = new AnyValue { StringValue = logBody },
                                    Attributes =
                                    {
                                        StringAttribute("browser.language", "en-US"),
                                        IntAttribute("browser.viewport.height", 844),
                                        IntAttribute("browser.viewport.width", 390),
                                        StringAttribute("navigation.type", "navigate"),
                                        StringAttribute("page.route", "/docs/mcp/"),
                                        StringAttribute("web.vital.name", "LCP"),
                                        StringAttribute("web.vital.rating", "good"),
                                        StringAttribute("web.vital.unit", "ms"),
                                        IntAttribute("web.vital.value", 778)
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        var attributes = request.ResourceLogs[0].ScopeLogs[0].LogRecords[0].Attributes;
        if (canonicalBrowserEvent)
        {
            attributes.Clear();
            attributes.Add([

                StringAttribute("browser.language", "en-US"),
                StringAttribute("url.path", "/docs/mcp/"),
                StringAttribute("browser.web_vital.id", "v5-test-id"),
                StringAttribute("browser.web_vital.name", "lcp"),
                StringAttribute("browser.web_vital.rating", "good"),
                StringAttribute("browser.web_vital.navigation_type", "navigate"),
                new KeyValue { Key = "browser.web_vital.delta", Value = new AnyValue { DoubleValue = 12.5 } },
                IntAttribute("browser.web_vital.value", 778)
            ]);
        }
        attributes.Add(StringAttribute("url.full", "https://example.test/private?token=secret"));
        attributes.Add(StringAttribute("url.query", "token=secret"));
        attributes.Add(StringAttribute("http.request.header.authorization", "Bearer secret"));

        var rows = IngestionStorageMapper.ToLogStorageRows(OtlpConverter.ConvertLogs(request,
            new FakeTimeProvider(CollectorApiTestHost.FixtureUtcNow)));
        var converted = Assert.ContainsSingle(rows);
        Assert.AreEqual(eventName, converted.EventName);
        Assert.AreEqual(TraceId, converted.TraceId);
        Assert.AreEqual(SpanId, converted.SpanId);
        // The stored body is the value's canonical JSON, so a string body carries its quotes.
        Assert.AreEqual(
            JsonSerializer.Serialize(new Api.Contracts.Common.AttributeValue.StringValue(logBody), global::Qyl.Api.Contracts.ContractTypes.TypeInfo<global::Qyl.Api.Contracts.Common.AttributeValue>()),
            converted.Body);
        Assert.IsNotNull(converted.AttributesJson);
        Assert.Contains(
            $"\"{valueKey}\":{{\"type\":\"int\",\"value\":\"778\"}}",
            converted.AttributesJson,
            StringComparison.Ordinal);

        Assert.DoesNotContain("\"url.full\"", converted.AttributesJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"url.query\"", converted.AttributesJson, StringComparison.Ordinal);
        Assert.Contains("Bearer secret", converted.AttributesJson, StringComparison.Ordinal);
        if (canonicalBrowserEvent)
        {
            foreach (var key in new[] { "browser.web_vital.id", "browser.web_vital.delta", "browser.web_vital.name", "browser.web_vital.rating", "browser.web_vital.navigation_type" })
                Assert.Contains($"\"{key}\"", converted.AttributesJson, StringComparison.Ordinal);
        }

        await using var store = new DuckDbStore(":memory:");
        await store.InsertLogsAsync(rows, testContext.CancellationToken);

        var context = CreateContext();
        var result = await CollectorEndpointExtensions.GetLogsAsync(
            context,
            store,
            sessionId: null,
            traceId: null,
            serviceName: null,
            level: null,
            query: null,
            testContext.CancellationToken);
        await result.ExecuteAsync(context);

        Assert.AreEqual(StatusCodes.Status200OK, context.Response.StatusCode);
        var restJson = await ReadBodyAsync(context);
        Assert.Contains($"\"event_name\":\"{eventName}\"", restJson, StringComparison.Ordinal);
        Assert.Contains($"\"trace_id\":\"{TraceId}\"", restJson, StringComparison.Ordinal);
        Assert.Contains($"\"span_id\":\"{SpanId}\"", restJson, StringComparison.Ordinal);
        Assert.Contains(logBody, restJson, StringComparison.Ordinal);
        Assert.Contains($"\"key\":\"{routeKey}\"", restJson, StringComparison.Ordinal);
        Assert.Contains("\"value\":\"/docs/mcp/\"", restJson, StringComparison.Ordinal);
        Assert.Contains($"\"key\":\"{valueKey}\"", restJson, StringComparison.Ordinal);
        Assert.Contains("778", restJson, StringComparison.Ordinal);
        var page = JsonSerializer.Deserialize(restJson, global::Qyl.Api.Contracts.ContractTypes.TypeInfo<global::Qyl.Api.Contracts.Common.Pagination.CursorPageLogRecord>());
        Assert.AreEqual(eventName, Assert.ContainsSingle(Assert.IsExactInstanceOfType<CursorPageLogRecord>(page).Items).EventName);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            testContext.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        await using var stream = CollectorEndpointExtensions.StreamLogEventsAsync(
                store,
                new FakeTimeProvider(),
                "default",
                serviceName: null,
                minSeverity: null,
                query: null,
                afterIngestSequence: null,
                timeout.Token)
            .GetAsyncEnumerator(timeout.Token);

        Assert.IsTrue(await stream.MoveNextAsync());
        var streamEvent = Assert.IsExactInstanceOfType<LogStreamEvent>(stream.Current.Log);
        Assert.AreEqual(eventName, streamEvent.Data.EventName);
        var streamJson = JsonSerializer.Serialize(streamEvent, global::Qyl.Api.Contracts.ContractTypes.TypeInfo<global::Qyl.Api.Contracts.Streaming.LogStreamEvent>());
        Assert.Contains($"\"event_name\":\"{eventName}\"", streamJson, StringComparison.Ordinal);
        Assert.Contains($"\"trace_id\":\"{TraceId}\"", streamJson, StringComparison.Ordinal);
        Assert.Contains($"\"span_id\":\"{SpanId}\"", streamJson, StringComparison.Ordinal);
        Assert.Contains(logBody, streamJson, StringComparison.Ordinal);
        Assert.Contains($"\"key\":\"{routeKey}\"", streamJson, StringComparison.Ordinal);
        Assert.Contains("\"value\":\"/docs/mcp/\"", streamJson, StringComparison.Ordinal);
        Assert.Contains($"\"key\":\"{valueKey}\"", streamJson, StringComparison.Ordinal);
        Assert.Contains("778", streamJson, StringComparison.Ordinal);
        foreach (var json in new[] { restJson, streamJson })
        {
            Assert.DoesNotContain("\"key\":\"url.full\"", json, StringComparison.Ordinal);
            Assert.DoesNotContain("\"key\":\"url.query\"", json, StringComparison.Ordinal);
            Assert.Contains("\"key\":\"http.request.header.authorization\",\"value\":\"Bearer secret\"", json, StringComparison.Ordinal);
        }
        if (canonicalBrowserEvent)
        {
            foreach (var key in new[] { "browser.web_vital.id", "browser.web_vital.delta", "browser.web_vital.name", "browser.web_vital.rating", "browser.web_vital.navigation_type" })
            {
                Assert.Contains($"\"key\":\"{key}\"", restJson, StringComparison.Ordinal);
                Assert.Contains($"\"key\":\"{key}\"", streamJson, StringComparison.Ordinal);
            }
        }
    }

    [TestMethod]
    [DataRow("CLS")]
    [DataRow("a quote: \"hello\"")]
    [DataRow("{\"unfinished\":")]
    [DataRow("123")]
    [DataRow("")]
    [DataRow("  ")]
    public async Task Historical_plain_text_bodies_remain_readable_in_rest_and_sse(string body)
    {
        await using var store = new DuckDbStore(":memory:");
        await store.InsertLogsAsync([
            new LogStorageRow
            {
                ProjectId = "default",
                LogId = "historical-body",
                TimeUnixNano = 2_000_000_000,
                SeverityNumber = 9,
                SeverityText = "INFO",
                Body = body,
                ServiceName = "qyl.at",
                EventName = "web.vitals.CLS"
            }
        ], testContext.CancellationToken);

        var context = CreateContext();
        var result = await CollectorEndpointExtensions.GetLogsAsync(
            context, store, sessionId: null, traceId: null, serviceName: "qyl.at",
            level: null, query: null, testContext.CancellationToken);
        await result.ExecuteAsync(context);
        Assert.AreEqual(StatusCodes.Status200OK, context.Response.StatusCode);
        var page = JsonSerializer.Deserialize(await ReadBodyAsync(context),
            global::Qyl.Api.Contracts.ContractTypes.TypeInfo<global::Qyl.Api.Contracts.Common.Pagination.CursorPageLogRecord>());
        var record = Assert.ContainsSingle(Assert.IsExactInstanceOfType<CursorPageLogRecord>(page).Items);
        Assert.AreEqual(body, Assert.IsExactInstanceOfType<Api.Contracts.Common.AttributeValue.StringValue>(record.Body).Value);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(testContext.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        await using var stream = CollectorEndpointExtensions.StreamLogEventsAsync(
            store, new FakeTimeProvider(), "default", serviceName: "qyl.at", minSeverity: null, query: null,
            afterIngestSequence: null, timeout.Token).GetAsyncEnumerator(timeout.Token);
        Assert.IsTrue(await stream.MoveNextAsync());
        var streamed = Assert.IsExactInstanceOfType<LogStreamEvent>(stream.Current.Log).Data;
        Assert.AreEqual(body, Assert.IsExactInstanceOfType<Api.Contracts.Common.AttributeValue.StringValue>(streamed.Body).Value);
    }

    private static KeyValue StringAttribute(string key, string value) =>
        new() { Key = key, Value = new AnyValue { StringValue = value } };

    private static KeyValue IntAttribute(string key, long value) =>
        new() { Key = key, Value = new AnyValue { IntValue = value } };

    private static DefaultHttpContext CreateContext()
    {
        return new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddLogging()
                .ConfigureHttpJsonOptions(static options =>
                    options.SerializerOptions.TypeInfoResolverChain.Insert(0, QylSerializerContext.Default))
                .BuildServiceProvider(),
            Response =
            {
                Body = new MemoryStream()
            }
        };
    }

    private async Task<string> ReadBodyAsync(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync(testContext.CancellationToken);
    }
}
