using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
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

public sealed class LogEventContractTests
{
    [Fact]
    public async Task Otlp_event_name_and_body_survive_storage_rest_and_sse()
    {
        const string eventName = "mcp.request";
        const string logBody = "MCP JSON-RPC request completed";
        const string traceId = "11111111111111111111111111111111";
        const string spanId = "2222222222222222";
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
                                    TraceId = Google.Protobuf.ByteString.CopyFrom(Convert.FromHexString(traceId)),
                                    SpanId = Google.Protobuf.ByteString.CopyFrom(Convert.FromHexString(spanId)),
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

        var rows = IngestionStorageMapper.ToLogStorageRows(OtlpConverter.ConvertLogs(request));
        var converted = Assert.Single(rows);
        Assert.Equal(eventName, converted.EventName);
        Assert.Equal(traceId, converted.TraceId);
        Assert.Equal(spanId, converted.SpanId);
        Assert.Equal(logBody, converted.Body);
        Assert.Contains(
            "\"web.vital.value\":{\"type\":\"int\",\"value\":\"778\"}",
            converted.AttributesJson,
            StringComparison.Ordinal);

        await using var store = new DuckDbStore(":memory:");
        await store.InsertLogsAsync(rows, TestContext.Current.CancellationToken);

        var context = CreateContext();
        var result = await CollectorEndpointExtensions.GetLogsAsync(
            context,
            store,
            sessionId: null,
            traceId: null,
            serviceName: null,
            level: null,
            query: null,
            TestContext.Current.CancellationToken);
        await result.ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        var restJson = await ReadBodyAsync(context);
        Assert.Contains($"\"event_name\":\"{eventName}\"", restJson, StringComparison.Ordinal);
        Assert.Contains($"\"trace_id\":\"{traceId}\"", restJson, StringComparison.Ordinal);
        Assert.Contains($"\"span_id\":\"{spanId}\"", restJson, StringComparison.Ordinal);
        Assert.Contains(logBody, restJson, StringComparison.Ordinal);
        Assert.Contains("\"key\":\"page.route\"", restJson, StringComparison.Ordinal);
        Assert.Contains("\"value\":\"/docs/mcp/\"", restJson, StringComparison.Ordinal);
        Assert.Contains("\"key\":\"web.vital.value\"", restJson, StringComparison.Ordinal);
        Assert.Contains("778", restJson, StringComparison.Ordinal);
        var page = JsonSerializer.Deserialize(restJson, QylSerializerContext.Default.CursorPageLogRecord);
        Assert.Equal(eventName, Assert.Single(Assert.IsType<CursorPageLogRecord>(page).Items).EventName);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        await using var stream = CollectorEndpointExtensions.StreamLogEventsAsync(
                store,
                "default",
                serviceName: null,
                minSeverity: null,
                query: null,
                afterIngestSequence: null,
                timeout.Token)
            .GetAsyncEnumerator(timeout.Token);

        Assert.True(await stream.MoveNextAsync());
        var streamEvent = Assert.IsType<LogStreamEvent>(stream.Current.Log);
        Assert.Equal(eventName, streamEvent.Data.EventName);
        var streamJson = JsonSerializer.Serialize(streamEvent, QylSerializerContext.Default.LogStreamEvent);
        Assert.Contains($"\"event_name\":\"{eventName}\"", streamJson, StringComparison.Ordinal);
        Assert.Contains($"\"trace_id\":\"{traceId}\"", streamJson, StringComparison.Ordinal);
        Assert.Contains($"\"span_id\":\"{spanId}\"", streamJson, StringComparison.Ordinal);
        Assert.Contains(logBody, streamJson, StringComparison.Ordinal);
        Assert.Contains("\"key\":\"page.route\"", streamJson, StringComparison.Ordinal);
        Assert.Contains("\"value\":\"/docs/mcp/\"", streamJson, StringComparison.Ordinal);
        Assert.Contains("\"key\":\"web.vital.value\"", streamJson, StringComparison.Ordinal);
        Assert.Contains("778", streamJson, StringComparison.Ordinal);
    }

    private static KeyValue StringAttribute(string key, string value) =>
        new() { Key = key, Value = new AnyValue { StringValue = value } };

    private static KeyValue IntAttribute(string key, long value) =>
        new() { Key = key, Value = new AnyValue { IntValue = value } };

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider()
        };
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
