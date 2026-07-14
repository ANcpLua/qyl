using OpenTelemetry.Proto.Collector.Trace.V1;

namespace Qyl.Collector.Grpc;

internal sealed class TraceServiceImpl(IQylStore store)
    : TraceService.TraceServiceBase
{
    public override Task<ExportTraceServiceResponse> Export(
        ExportTraceServiceRequest request,
        ServerCallContext context) =>
        GrpcExport.ExecuteAsync(async () =>
        {
            var traceBatch = OtlpConverter.ConvertTraceRequest(request);
            var spans = IngestionStorageMapper.ToSpanStorageRows(traceBatch);

            if (spans.Count <= 0) return new ExportTraceServiceResponse();

            await store.EnqueueAsync(new SpanBatch(spans), context.CancellationToken).ConfigureAwait(false);

            return new ExportTraceServiceResponse();
        }, "trace");
}
