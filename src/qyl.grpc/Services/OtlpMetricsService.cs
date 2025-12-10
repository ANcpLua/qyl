using Grpc.Core;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Proto.Collector.Metrics.V1;
using qyl.grpc.Models;
using qyl.grpc.Protocol;

namespace qyl.grpc.Services;

public sealed class OtlpMetricsService(
    OtlpConverter converter,
    ILogger<OtlpMetricsService> logger) : MetricsService.MetricsServiceBase
{
    /// <summary>
    /// Event raised when metrics are received.
    /// </summary>
    public event Action<IReadOnlyList<MetricModel>>? MetricsReceived;

    public override Task<ExportMetricsServiceResponse> Export(
        ExportMetricsServiceRequest request,
        ServerCallContext context)
    {
        var metrics = converter.ConvertResourceMetrics(request.ResourceMetrics).ToList();

        if (metrics.Count > 0)
        {
            logger.LogDebug("Received {MetricCount} metrics", metrics.Count);
            MetricsReceived?.Invoke(metrics);
        }

        return Task.FromResult(new ExportMetricsServiceResponse());
    }
}
