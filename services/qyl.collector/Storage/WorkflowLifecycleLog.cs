namespace Qyl.Collector.Storage;

internal static partial class WorkflowLifecycleLog
{
    [LoggerMessage(
        EventId = 4100,
        Level = LogLevel.Warning,
        Message = "Workflow projection admission rejected ({Reason})")]
    public static partial void ProjectionAdmissionRejected(
        ILogger logger,
        string reason);

    [LoggerMessage(
        EventId = 4101,
        Level = LogLevel.Warning,
        Message = "Workflow projection retry {Attempt} ({Reason})")]
    public static partial void ProjectionRetry(
        ILogger logger,
        int attempt,
        string reason);

    [LoggerMessage(
        EventId = 4102,
        Level = LogLevel.Error,
        Message = "Workflow projection failed ({Reason})")]
    public static partial void ProjectionFailed(
        ILogger logger,
        string reason);

    [LoggerMessage(
        EventId = 4103,
        Level = LogLevel.Error,
        Message = "Workflow projection worker {WorkerIndex} stopped unexpectedly ({Reason})")]
    public static partial void ProjectionWorkerFailed(
        ILogger logger,
        int workerIndex,
        string reason);

    [LoggerMessage(
        EventId = 4104,
        Level = LogLevel.Warning,
        Message = "Workflow checkpoint reconciliation deferred ({Reason})")]
    public static partial void ReconciliationDeferred(
        ILogger logger,
        string reason);

    [LoggerMessage(
        EventId = 4105,
        Level = LogLevel.Error,
        Message = "Workflow storage shutdown failed ({Reason})")]
    public static partial void ShutdownFailed(
        ILogger logger,
        string reason);

    [LoggerMessage(
        EventId = 4106,
        Level = LogLevel.Error,
        Message = "Workflow storage worker stopped unexpectedly ({Reason})")]
    public static partial void StorageWorkerFailed(
        ILogger logger,
        string reason);
}
