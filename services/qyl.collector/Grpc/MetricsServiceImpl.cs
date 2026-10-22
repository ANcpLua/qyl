using Microsoft.Extensions.Options;
using OpenTelemetry.Proto.Collector.Metrics.V1;
using Qyl.Collector.ApiKeys;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Grpc;

internal sealed class MetricsServiceImpl(IQylStore store, IOptions<OtlpApiKeyOptions>? options = null)
{
    public Task<ExportMetricsServiceResponse> Export(
        ExportMetricsServiceRequest request,
        ServerCallContext context) =>
        GrpcExport.ExecuteAsync(async () =>
        {
            var batch = OtlpConverter.ConvertMetrics(request);
            var write = MetricStorageMapper.ToStorageRows(
                batch, AuthenticatedProject.ForGrpcIngest(context, options?.Value));

            if (write.Observations.Count > 0)
            {
                await store
                    .InsertMetricsAsync(write.Series, write.Observations, context.CancellationToken)
                    .ConfigureAwait(false);
            }

            return OtlpExportAck.Create(batch);
        }, "metric");
}
