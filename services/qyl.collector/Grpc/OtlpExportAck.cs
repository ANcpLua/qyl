using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Metrics.V1;
using OpenTelemetry.Proto.Collector.Trace.V1;

namespace Qyl.Collector.Grpc;

/// <summary>Both transports acknowledge the same accepted records and rejected-item diagnostics.</summary>
internal static class OtlpExportAck
{
    public static ExportTraceServiceResponse Create(TraceIngestionBatch batch) => batch.RejectedSpans == 0
        ? new ExportTraceServiceResponse()
        : new ExportTraceServiceResponse { PartialSuccess = new ExportTracePartialSuccess
        { RejectedSpans = batch.RejectedSpans, ErrorMessage = batch.RejectionMessage ?? throw new InvalidDataException("Rejected spans require diagnostics.") } };

    public static ExportLogsServiceResponse Create(LogIngestionBatch batch) => batch.RejectedLogRecords == 0
        ? new ExportLogsServiceResponse()
        : new ExportLogsServiceResponse { PartialSuccess = new ExportLogsPartialSuccess
        { RejectedLogRecords = batch.RejectedLogRecords, ErrorMessage = batch.RejectionMessage ?? throw new InvalidDataException("Rejected logs require diagnostics.") } };

    public static ExportMetricsServiceResponse Create(MetricIngestionBatch batch) => batch.RejectedDataPoints == 0
        ? new ExportMetricsServiceResponse()
        : new ExportMetricsServiceResponse { PartialSuccess = new ExportMetricsPartialSuccess
        { RejectedDataPoints = batch.RejectedDataPoints, ErrorMessage = batch.RejectionMessage ?? throw new InvalidDataException("Rejected metrics require diagnostics.") } };
}
