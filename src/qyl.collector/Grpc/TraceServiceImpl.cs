using StatusCode = Grpc.Core.StatusCode;

namespace qyl.collector.Grpc;

/// <summary>
///     gRPC implementation of the OTLP TraceService for span ingestion on port 4317.
///     Uses OtlpConverter for conversion (shared with HTTP endpoint).
/// </summary>
public sealed class TraceServiceImpl(DuckDbStore store, ITelemetrySseBroadcaster broadcaster, SpanRingBuffer ringBuffer)
    : TraceServiceBase
{
    private readonly ITelemetrySseBroadcaster _broadcaster = Guard.NotNull(broadcaster);
    private readonly SpanRingBuffer _ringBuffer = Guard.NotNull(ringBuffer);
    private readonly DuckDbStore _store = Guard.NotNull(store);

    /// <summary>
    ///     Implements opentelemetry.proto.collector.trace.v1.TraceService.Export
    /// </summary>
    public override async Task<ExportTraceServiceResponse> Export(
        ExportTraceServiceRequest request,
        ServerCallContext context)
    {
        try
        {
            var spans = OtlpConverter.ConvertProtoToStorageRows(request);

            if (spans.Count <= 0) return new ExportTraceServiceResponse();

            // Apply Codex telemetry transformations (codex.* -> gen_ai.*)
            var batch = new SpanBatch(spans).WithCodexTransformations();

            // Push to ring buffer for real-time queries
            _ringBuffer.PushRange([.. batch.Spans.Select(SpanMapper.ToRecord)]);

            await _store.EnqueueAsync(batch, context.CancellationToken).ConfigureAwait(false);
            _broadcaster.PublishSpans(batch);

            return new ExportTraceServiceResponse();
        }
        catch (OperationCanceledException)
        {
            throw new RpcException(new Status(StatusCode.Cancelled, "Request was cancelled"));
        }
        catch (ObjectDisposedException)
        {
            throw new RpcException(new Status(StatusCode.Unavailable, "Service is shutting down"));
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, $"Failed to process trace data: {ex.Message}"));
        }
    }
}
