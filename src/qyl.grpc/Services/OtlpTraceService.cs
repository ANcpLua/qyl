using Grpc.Core;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Proto.Collector.Trace.V1;
using qyl.Grpc.Models;
using qyl.Grpc.Protocol;

namespace qyl.Grpc.Services;

public sealed class OtlpTraceService(
    OtlpConverter converter,
    ILogger<OtlpTraceService> logger) : TraceService.TraceServiceBase
{
    /// <summary>
    /// Event raised when spans are received.
    /// </summary>
    public event Action<IReadOnlyList<SpanModel>>? SpansReceived;

    public override Task<ExportTraceServiceResponse> Export(
        ExportTraceServiceRequest request,
        ServerCallContext context)
    {
        var spans = converter.ConvertResourceSpans(request.ResourceSpans).ToList();

        if (spans.Count > 0)
        {
            logger.LogDebug("Received {SpanCount} spans", spans.Count);
            SpansReceived?.Invoke(spans);
        }

        return Task.FromResult(new ExportTraceServiceResponse());
    }
}
