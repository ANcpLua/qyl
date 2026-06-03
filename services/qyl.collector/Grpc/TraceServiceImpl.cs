using Qyl.Collector.Cost;
using OpenTelemetry.Proto.Collector.Trace.V1;
using StatusCode = Grpc.Core.StatusCode;

namespace Qyl.Collector.Grpc;

internal sealed class TraceServiceImpl(
    IQylStore store,
    ModelPricingService pricingService)
    : TraceService.TraceServiceBase
{
    private readonly ModelPricingService _pricingService =
        pricingService ?? throw new ArgumentNullException(nameof(pricingService));
    private readonly IQylStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public override async Task<ExportTraceServiceResponse> Export(
        ExportTraceServiceRequest request,
        ServerCallContext context)
    {
        try
        {
            var traceBatch = OtlpConverter.ConvertTraceRequest(request);
            var spans = IngestionStorageMapper.ToSpanStorageRows(traceBatch);

            if (spans.Count <= 0) return new ExportTraceServiceResponse();

            var batch = _pricingService.EnrichBatchWithCost(new SpanBatch(spans));

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
        catch (Exception)
        {
            throw new RpcException(new Status(StatusCode.Internal, "Failed to process trace data."));
        }
    }
}
