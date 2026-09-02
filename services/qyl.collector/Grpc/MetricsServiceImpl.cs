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
            var batch = OtlpConverter.ConvertMetrics(request);
            var write = MetricStorageMapper.ToStorageRows(batch);

            if (write.Points.Count > 0)
            {
                await store
                    .InsertMetricsAsync(write.Series, write.Points, context.CancellationToken)
                    .ConfigureAwait(false);
            }

            return MetricsExportAck.Create(batch);
        }, "metric");
}

/// <summary>
/// Builds the one OTLP response shape both transports return, so a shape qyl declines is
/// reported identically over gRPC and HTTP.
/// </summary>
internal static class MetricsExportAck
{
    public static ExportMetricsServiceResponse Create(MetricIngestionBatch batch)
    {
        if (batch.RejectedDataPoints is 0)
            return new ExportMetricsServiceResponse();

        return new ExportMetricsServiceResponse
        {
            PartialSuccess = new ExportMetricsPartialSuccess
            {
                RejectedDataPoints = batch.RejectedDataPoints,
                ErrorMessage = batch.RejectionMessage ?? ""
            }
        };
    }
}
