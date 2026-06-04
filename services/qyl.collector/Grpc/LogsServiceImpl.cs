using OpenTelemetry.Proto.Collector.Logs.V1;

namespace Qyl.Collector.Grpc;

internal sealed class LogsServiceImpl(IQylStore store)
    : LogsService.LogsServiceBase
{
    public override Task<ExportLogsServiceResponse> Export(
        ExportLogsServiceRequest request,
        ServerCallContext context) =>
        GrpcExport.ExecuteAsync(async () =>
        {
            var logBatch = OtlpConverter.ConvertLogs(request);
            var logs = IngestionStorageMapper.ToLogStorageRows(logBatch);

            if (logs.Count <= 0) return new ExportLogsServiceResponse();

            await store.InsertLogsAsync(logs, context.CancellationToken).ConfigureAwait(false);
            return new ExportLogsServiceResponse();
        }, "log");
}
