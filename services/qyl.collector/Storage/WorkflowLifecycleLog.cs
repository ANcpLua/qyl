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
        Message = "Workflow projection retry {Attempt}")]
    public static partial void ProjectionRetry(
        ILogger logger,
        int attempt,
        Exception error);

    [LoggerMessage(
        EventId = 4102,
        Level = LogLevel.Error,
        Message = "Workflow projection failed")]
    public static partial void ProjectionFailed(
        ILogger logger,
        Exception error);

    [LoggerMessage(
        EventId = 4104,
        Level = LogLevel.Warning,
        Message = "Workflow checkpoint reconciliation deferred")]
    public static partial void ReconciliationDeferred(
        ILogger logger,
        Exception error);

    [LoggerMessage(
        EventId = 4105,
        Level = LogLevel.Error,
        Message = "Workflow storage shutdown failed")]
    public static partial void ShutdownFailed(
        ILogger logger,
        Exception error);

    [LoggerMessage(
        EventId = 4106,
        Level = LogLevel.Error,
        Message = "Workflow storage worker stopped unexpectedly")]
    public static partial void StorageWorkerFailed(
        ILogger logger,
        Exception error);

    [LoggerMessage(
        EventId = 4107,
        Level = LogLevel.Error,
        Message = "Workflow checkpoint reconciliation failed; the collector keeps ingesting")]
    public static partial void ReconciliationFailed(
        ILogger logger,
        Exception error);
}
