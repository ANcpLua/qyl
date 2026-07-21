using OpenTelemetry.Proto.Collector.Metrics.V1;

namespace Qyl.Collector.Grpc;

internal sealed class MetricsServiceImpl
    : MetricsService.MetricsServiceBase
{
    public override Task<ExportMetricsServiceResponse> Export(
        ExportMetricsServiceRequest request,
        ServerCallContext context) =>
        Task.FromResult(OtlpMetricsDiscard.CreateResponse(request));
}
