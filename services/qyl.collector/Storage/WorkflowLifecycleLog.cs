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

    [LoggerMessage(
        EventId = 4108,
        Level = LogLevel.Information,
        Message = "Workflow journal append committed: accepted={AcceptedCount}, duplicates={DuplicateCount}, elapsed_ms={ElapsedMilliseconds}")]
    public static partial void JournalAppendCommitted(
        ILogger logger,
        int acceptedCount,
        int duplicateCount,
        double elapsedMilliseconds);

    [LoggerMessage(
        EventId = 4109,
        Level = LogLevel.Debug,
        Message = "Workflow projection queued at journal position {TargetJournalPosition}")]
    public static partial void ProjectionQueued(
        ILogger logger,
        ulong targetJournalPosition);

    [LoggerMessage(
        EventId = 4110,
        Level = LogLevel.Debug,
        Message = "Workflow projection demand coalesced at journal position {TargetJournalPosition}")]
    public static partial void ProjectionCoalesced(
        ILogger logger,
        ulong targetJournalPosition);

    [LoggerMessage(
        EventId = 4111,
        Level = LogLevel.Debug,
        Message = "Workflow projection started: mode={Mode}, from={FromJournalPosition}, target={TargetJournalPosition}")]
    public static partial void ProjectionStarted(
        ILogger logger,
        string mode,
        ulong fromJournalPosition,
        ulong targetJournalPosition);

    [LoggerMessage(
        EventId = 4112,
        Level = LogLevel.Information,
        Message = "Workflow projection completed: mode={Mode}, journal_events={JournalEvents}, journal_position={JournalPosition}")]
    public static partial void ProjectionCompleted(
        ILogger logger,
        string mode,
        ulong journalEvents,
        ulong journalPosition);

    [LoggerMessage(
        EventId = 4113,
        Level = LogLevel.Debug,
        Message = "Workflow projection cancelled")]
    public static partial void ProjectionCancelled(ILogger logger);

    [LoggerMessage(
        EventId = 4114,
        Level = LogLevel.Information,
        Message = "Workflow projection generation retired")]
    public static partial void ProjectionRetired(ILogger logger);

    [LoggerMessage(
        EventId = 4115,
        Level = LogLevel.Information,
        Message = "Workflow checkpoint written: bytes={CheckpointBytes}")]
    public static partial void CheckpointWritten(
        ILogger logger,
        long checkpointBytes);

    [LoggerMessage(
        EventId = 4116,
        Level = LogLevel.Warning,
        Message = "Workflow checkpoint validation failed: reason={Reason}, count={Count}")]
    public static partial void CheckpointValidationFailed(
        ILogger logger,
        string reason,
        int count);

    [LoggerMessage(
        EventId = 4117,
        Level = LogLevel.Information,
        Message = "Workflow checkpoint compare-and-swap publication: outcome={Outcome}")]
    public static partial void CheckpointPublication(
        ILogger logger,
        string outcome);

    [LoggerMessage(
        EventId = 4118,
        Level = LogLevel.Information,
        Message = "Workflow checkpoint reconciliation scheduled {RepairCount} repair(s)")]
    public static partial void ReconciliationRepairs(
        ILogger logger,
        int repairCount);

    [LoggerMessage(
        EventId = 4119,
        Level = LogLevel.Information,
        Message = "Workflow checkpoint reconciliation removed {RemovedCount} orphan(s), bytes={RemovedBytes}")]
    public static partial void OrphansRemoved(
        ILogger logger,
        int removedCount,
        long removedBytes);

    [LoggerMessage(
        EventId = 4120,
        Level = LogLevel.Warning,
        Message = "DuckDB workflow operation failed: classification={Classification}, retry={Retry}")]
    public static partial void DuckDbFailure(
        ILogger logger,
        string classification,
        bool retry,
        Exception error);

    [LoggerMessage(
        EventId = 4121,
        Level = LogLevel.Debug,
        Message = "Workflow Arrow stream consumed: purpose={Purpose}, batches={Batches}, rows={Rows}")]
    public static partial void ArrowStreamConsumed(
        ILogger logger,
        string purpose,
        int batches,
        long rows);
}
