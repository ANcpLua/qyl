using OpenTelemetry.Proto.Collector.Metrics.V1;

namespace Qyl.Collector.Ingestion;

internal static class OtlpMetricsDiscard
{
    internal const string ErrorMessage = "metrics are accepted for wire compatibility but not stored";

    public static ExportMetricsServiceResponse CreateResponse(ExportMetricsServiceRequest request)
    {
        long rejectedDataPoints = 0;

        foreach (var resource in request.ResourceMetrics)
        foreach (var scope in resource.ScopeMetrics)
        foreach (var metric in scope.Metrics)
        {
            rejectedDataPoints += metric.Gauge?.DataPoints.Count ?? 0;
            rejectedDataPoints += metric.Sum?.DataPoints.Count ?? 0;
            rejectedDataPoints += metric.Histogram?.DataPoints.Count ?? 0;
            rejectedDataPoints += metric.ExponentialHistogram?.DataPoints.Count ?? 0;
            rejectedDataPoints += metric.Summary?.DataPoints.Count ?? 0;
        }

        return new ExportMetricsServiceResponse
        {
            PartialSuccess = new ExportMetricsPartialSuccess
            {
                RejectedDataPoints = rejectedDataPoints,
                ErrorMessage = ErrorMessage
            }
        };
    }
}
