using Qyl.Collector.Cost;
using OpenTelemetry.Proto.Collector.Trace.V1;
using StatusCode = Grpc.Core.StatusCode;

namespace Qyl.Collector.Grpc;

public sealed class TraceServiceImpl(
    DuckDbStore store,
    SpanRingBuffer ringBuffer,
    ModelPricingService pricingService)
    : TraceService.TraceServiceBase
{
    private readonly ModelPricingService _pricingService = Guard.NotNull(pricingService);
    private readonly SpanRingBuffer _ringBuffer = Guard.NotNull(ringBuffer);
    private readonly DuckDbStore _store = Guard.NotNull(store);

    public override async Task<ExportTraceServiceResponse> Export(
        ExportTraceServiceRequest request,
        ServerCallContext context)
    {
        try
        {
            var spans = OtlpConverter.ConvertTraceRequestToStorageRows(request);

            if (spans.Count <= 0) return new ExportTraceServiceResponse();

            var batch = _pricingService.EnrichBatchWithCost(
                new SpanBatch(spans).WithCodexTransformations());

            _ringBuffer.PushRange(batch.Spans);

            await _store.EnqueueAsync(batch, context.CancellationToken).ConfigureAwait(false);

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
