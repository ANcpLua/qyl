using OpenTelemetry.Proto.Collector.Metrics.V1;

namespace Qyl.Collector.Grpc;

internal sealed class MetricsServiceImpl(IQylStore store)
    : MetricsService.MetricsServiceBase
{
    public override Task<ExportMetricsServiceResponse> Export(
        ExportMetricsServiceRequest request,
        ServerCallContext context) =>
        GrpcExport.ExecuteAsync(async () =>
        {
            var metricBatch = OtlpConverter.ConvertMetrics(request);
            var metrics = IngestionStorageMapper.ToMetricStorageRows(metricBatch);

            if (metrics.Count <= 0) return new ExportMetricsServiceResponse();

            await store.InsertMetricsAsync(metrics, context.CancellationToken).ConfigureAwait(false);
            return new ExportMetricsServiceResponse();
        }, "metric");
}
