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
    public async Task Otlp_event_name_survives_storage_rest_and_sse_while_body_stays_redacted()
    {
        const string eventName = "gen_ai.evaluation.result";
        const string sensitiveBody = "customer-secret-evaluation-payload";
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
                                    Body = new AnyValue { StringValue = sensitiveBody }
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
        Assert.StartsWith("sha256:", converted.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(sensitiveBody, converted.Body, StringComparison.Ordinal);

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
        Assert.DoesNotContain(sensitiveBody, restJson, StringComparison.Ordinal);
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
        Assert.DoesNotContain(sensitiveBody, streamJson, StringComparison.Ordinal);
    }

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
