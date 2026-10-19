using OpenTelemetry.Proto.Collector.Trace.V1;
using Qyl.Collector.ApiKeys;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Grpc;

internal sealed class TraceServiceImpl(IQylStore store, OtlpApiKeyOptions? options = null)
{
    public Task<ExportTraceServiceResponse> Export(
        ExportTraceServiceRequest request,
        ServerCallContext context) =>
        GrpcExport.ExecuteAsync(async () =>
        {
            var traceBatch = OtlpConverter.ConvertTraceRequest(request);
            var spans = IngestionStorageMapper.ToSpanStorageRows(
                traceBatch, AuthenticatedProject.ForGrpcIngest(context, options));

            if (spans.Count <= 0) return OtlpExportAck.Create(traceBatch);

            await store.EnqueueAsync(new SpanBatch(spans), context.CancellationToken).ConfigureAwait(false);

            return OtlpExportAck.Create(traceBatch);
        }, "trace");
}
