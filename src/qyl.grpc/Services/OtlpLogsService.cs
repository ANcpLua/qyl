using Grpc.Core;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Proto.Collector.Logs.V1;
using qyl.grpc.Models;
using qyl.grpc.Protocol;

namespace qyl.grpc.Services;

public sealed class OtlpLogsService(
    OtlpConverter converter,
    ILogger<OtlpLogsService> logger) : LogsService.LogsServiceBase
{
    /// <summary>
    /// Event raised when logs are received.
    /// </summary>
    public event Action<IReadOnlyList<LogModel>>? LogsReceived;

    public override Task<ExportLogsServiceResponse> Export(
        ExportLogsServiceRequest request,
        ServerCallContext context)
    {
        var logs = converter.ConvertResourceLogs(request.ResourceLogs).ToList();

        if (logs.Count > 0)
        {
            logger.LogDebug("Received {LogCount} logs", logs.Count);
            LogsReceived?.Invoke(logs);
        }

        return Task.FromResult(new ExportLogsServiceResponse());
    }
}
