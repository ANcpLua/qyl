using OpenTelemetry.Proto.Collector.Logs.V1;
using Qyl.Collector.ApiKeys;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Grpc;

internal sealed class LogsServiceImpl(IQylStore store, TimeProvider timeProvider, OtlpApiKeyOptions? options = null)
{
    public Task<ExportLogsServiceResponse> Export(
        ExportLogsServiceRequest request,
        ServerCallContext context) =>
        GrpcExport.ExecuteAsync(async () =>
        {
            var logBatch = OtlpConverter.ConvertLogs(request, timeProvider);
            var logs = IngestionStorageMapper.ToLogStorageRows(
                logBatch, AuthenticatedProject.ForGrpcIngest(context, options));

            if (logs.Count <= 0) return OtlpExportAck.Create(logBatch);

            await store.InsertLogsAsync(logs, context.CancellationToken).ConfigureAwait(false);
            return OtlpExportAck.Create(logBatch);
        }, "log");
}
