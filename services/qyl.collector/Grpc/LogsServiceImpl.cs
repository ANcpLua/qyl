using OpenTelemetry.Proto.Collector.Logs.V1;
using StatusCode = Grpc.Core.StatusCode;

namespace Qyl.Collector.Grpc;

internal sealed class LogsServiceImpl(DuckDbStore store)
    : LogsService.LogsServiceBase
{
    private readonly DuckDbStore _store = Guard.NotNull(store);

    public override async Task<ExportLogsServiceResponse> Export(
        ExportLogsServiceRequest request,
        ServerCallContext context)
    {
        try
        {
            var logBatch = OtlpConverter.ConvertLogs(request);
            var logs = IngestionStorageMapper.ToLogStorageRows(logBatch);

            if (logs.Count <= 0) return new ExportLogsServiceResponse();

            await _store.InsertLogsAsync(logs, context.CancellationToken).ConfigureAwait(false);
            return new ExportLogsServiceResponse();
        }
        catch (OperationCanceledException)
        {
            throw new RpcException(new Status(StatusCode.Cancelled, "Request was cancelled"));
        }
        catch (ObjectDisposedException)
        {
            throw new RpcException(new Status(StatusCode.Unavailable, "Service is shutting down"));
        }
        catch (Exception)
        {
            throw new RpcException(new Status(StatusCode.Internal, "Failed to process log data."));
        }
    }
}
