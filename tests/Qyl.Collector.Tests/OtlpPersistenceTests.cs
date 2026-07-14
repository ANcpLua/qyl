using Google.Protobuf;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;
using Qyl.Collector.Grpc;
using Qyl.Collector.Hosting;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Storage;
using RpcStatus = Google.Rpc.Status;

namespace Qyl.Collector.Tests;

public sealed class OtlpPersistenceTests
{
    [Fact]
    public async Task Http_persistence_failure_is_retryable_and_uses_the_official_status_envelope()
    {
        await using var store = new DuckDbStore(
            ":memory:",
            beforeWrite: _ => ValueTask.FromException(new IOException("Injected persistence failure.")));
        var request = TraceRequest(1);
        var payload = request.ToByteArray();
        var context = ResponseContext();
        context.Request.ContentType = OtlpPayloadParser.ProtobufContentType;
        context.Request.ContentLength = payload.Length;
        context.Request.Body = new MemoryStream(payload);
        var result = await CollectorEndpointExtensions.IngestOtlpTracesAsync(
            context,
            store,
            TestContext.Current.CancellationToken);
        await result.ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        var status = RpcStatus.Parser.ParseFrom(((MemoryStream)context.Response.Body).ToArray());
        Assert.Equal("The collector could not persist the OTLP traces payload.", status.Message);
    }

    [Fact]
    public async Task Http_sender_corruption_remains_a_non_retryable_bad_request()
    {
        await using var store = new DuckDbStore(":memory:");
        var request = TraceRequest(1);
        request.ResourceSpans[0].ScopeSpans[0].Spans[0].TraceId = ByteString.CopyFrom([1]);
        var payload = request.ToByteArray();
        var context = ResponseContext();
        context.Request.ContentType = OtlpPayloadParser.ProtobufContentType;
        context.Request.ContentLength = payload.Length;
        context.Request.Body = new MemoryStream(payload);
        var result = await CollectorEndpointExtensions.IngestOtlpTracesAsync(
            context,
            store,
            TestContext.Current.CancellationToken);
        await result.ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        var status = RpcStatus.Parser.ParseFrom(((MemoryStream)context.Response.Body).ToArray());
        Assert.Equal("The OTLP traces payload is invalid.", status.Message);
    }

    [Fact]
    public async Task Grpc_persistence_failure_is_unavailable_so_exporters_retry()
    {
        await using var store = new DuckDbStore(
            ":memory:",
            beforeWrite: _ => ValueTask.FromException(new IOException("Injected persistence failure.")));
        var batch = Batch(1);

        var error = await Assert.ThrowsAsync<RpcException>(() => GrpcExport.ExecuteAsync(async () =>
        {
            await store.EnqueueAsync(batch, TestContext.Current.CancellationToken);
            return new ExportTraceServiceResponse();
        }, "trace"));

        Assert.Equal(StatusCode.Unavailable, error.StatusCode);
    }

    [Fact]
    public async Task Full_write_queue_fails_fast_instead_of_dropping_an_acknowledged_trace_batch()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var store = new DuckDbStore(
            ":memory:",
            jobQueueCapacity: 1,
            beforeWrite: async token =>
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(token);
            });

        var first = store.EnqueueAsync(Batch(1), TestContext.Current.CancellationToken).AsTask();
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        var queued = store.EnqueueAsync(Batch(2), TestContext.Current.CancellationToken).AsTask();
        var saturated = store.EnqueueAsync(Batch(3), TestContext.Current.CancellationToken).AsTask();

        await Assert.ThrowsAsync<QylStoreUnavailableException>(() => saturated);
        release.TrySetResult();
        await Task.WhenAll(first, queued);

        var rows = await store.GetSpansAsync("default", ct: TestContext.Current.CancellationToken);
        Assert.Equal(2, rows.Count);
    }

    private static DefaultHttpContext ResponseContext()
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider()
        };
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static ExportTraceServiceRequest TraceRequest(int identity) => new()
    {
        ResourceSpans =
        {
            new ResourceSpans
            {
                Resource = new Resource(),
                ScopeSpans =
                {
                    new ScopeSpans
                    {
                        Spans =
                        {
                            new Span
                            {
                                TraceId = ByteString.CopyFrom(new byte[15].Append((byte)identity).ToArray()),
                                SpanId = ByteString.CopyFrom(new byte[7].Append((byte)identity).ToArray()),
                                Name = $"persist-{identity}",
                                StartTimeUnixNano = 1,
                                EndTimeUnixNano = 2
                            }
                        }
                    }
                }
            }
        }
    };

    private static SpanBatch Batch(int identity)
    {
        var request = TraceRequest(identity);
        return new SpanBatch(IngestionStorageMapper.ToSpanStorageRows(OtlpConverter.ConvertTraceRequest(request)));
    }
}
