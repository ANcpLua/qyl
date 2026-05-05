using Qyl.Collector.Cost;
using StatusCode = Grpc.Core.StatusCode;

namespace Qyl.Collector.Grpc;

public sealed class TraceServiceImpl(
    DuckDbStore store,
    ITelemetrySseBroadcaster broadcaster,
    SpanRingBuffer ringBuffer,
    ModelPricingService pricingService)
    : TraceServiceBase
{
    private readonly ITelemetrySseBroadcaster _broadcaster = Guard.NotNull(broadcaster);
    private readonly ModelPricingService _pricingService = Guard.NotNull(pricingService);
    private readonly SpanRingBuffer _ringBuffer = Guard.NotNull(ringBuffer);
    private readonly DuckDbStore _store = Guard.NotNull(store);

    public override async Task<ExportTraceServiceResponse> Export(
        ExportTraceServiceRequest request,
        ServerCallContext context)
    {
        try
        {
            var spans = OtlpConverter.ConvertProtoToStorageRows(request);

            if (spans.Count <= 0) return new ExportTraceServiceResponse();

            var batch = _pricingService.EnrichBatchWithCost(
                new SpanBatch(spans).WithCodexTransformations());

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
