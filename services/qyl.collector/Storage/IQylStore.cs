namespace Qyl.Collector.Storage;

internal sealed class QylStoreUnavailableException(string message) : Exception(message);

internal readonly record struct TracePageCursor(ulong ActivityUnixNano, string TraceId);

internal sealed record TraceStoragePage(
    IReadOnlyList<TraceStoragePageItem> Items,
    bool HasMore);

internal sealed record TraceStoragePageItem(
    string TraceId,
    ulong ActivityUnixNano,
    IReadOnlyList<SpanStorageRow> Spans);

internal readonly record struct StorageFileMetrics(long DatabaseFileSizeBytes, long StorageFreeBytes);

internal interface IQylStore : IAsyncDisposable
{
    ValueTask EnqueueAsync(SpanBatch batch, CancellationToken ct = default);

    Task InsertLogsAsync(IReadOnlyList<LogStorageRow> logs, CancellationToken ct = default);

    Task<int> DeleteExpiredLogsBatchAsync(
        ulong cutoffUnixNano,
        int batchSize,
        CancellationToken ct = default);

    Task<int> DeleteExpiredSpansBatchAsync(
        ulong cutoffUnixNano,
        int batchSize,
        CancellationToken ct = default);

    Task CheckpointAsync(CancellationToken ct = default);

    StorageFileMetrics GetStorageFileMetrics();

    Task<IReadOnlyList<SessionQueryRow>> GetSessionsAsync(
        string projectId,
        int limit = 100,
        int offset = 0,
        bool? isActive = null,
        DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null,
        CancellationToken ct = default);

    Task<SessionQueryRow?> GetSessionAsync(
        string sessionId,
        string projectId,
        CancellationToken ct = default);

    Task<SessionStatsRow> GetSessionStatsAsync(
        string projectId,
        DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<SpanStorageRow>> GetSpansBySessionAsync(
        string sessionId,
        string projectId,
        CancellationToken ct = default);

    Task<IReadOnlyList<SpanStorageRow>> GetTraceAsync(
        string traceId,
        string projectId,
        CancellationToken ct = default);

    Task<IReadOnlyList<SpanStorageRow>> GetSpansAsync(
        string projectId,
        int limit = 100,
        CancellationToken ct = default);

    Task<TraceStoragePage> GetTracePageAsync(
        string projectId,
        TracePageCursor? cursor,
        int limit,
        CancellationToken ct = default);

    Task<StorageStats> GetStorageStatsAsync(string projectId, CancellationToken ct = default);

    Task<IReadOnlyList<LogStorageRow>> GetLogsAsync(
        string projectId,
        string? sessionId = null,
        string? traceId = null,
        string? severityText = null,
        int? minSeverity = null,
        string? search = null,
        ulong? start = null,
        ulong? before = null,
        string? serviceName = null,
        int limit = 500,
        CancellationToken ct = default);

    Task<IReadOnlyList<LogStorageRow>> GetLogStreamPageAsync(
        string projectId,
        string? serviceName = null,
        int? minSeverity = null,
        string? search = null,
        long? afterIngestSequence = null,
        int limit = 250,
        CancellationToken ct = default);

    Task<WorkflowRunStorageRow> CreateWorkflowRunAsync(
        WorkflowRunStorageRow run,
        CancellationToken ct = default);

    Task<WorkflowRunStorageRow?> GetWorkflowRunAsync(
        string projectId,
        string runId,
        CancellationToken ct = default);

    Task<IReadOnlyList<WorkflowRunStorageRow>> ListWorkflowRunsAsync(
        string projectId,
        Qyl.Api.Contracts.Workflow.WorkflowRunStatus? status,
        int limit,
        int offset,
        CancellationToken ct = default);

    Task<WorkflowAppendResult> AppendWorkflowEventsAsync(
        string projectId,
        string runId,
        string clientId,
        IReadOnlyList<WorkflowEventWrite> events,
        IReadOnlyList<WorkflowContentWrite> content,
        CancellationToken ct = default);

    Task<WorkflowEventStoragePage?> ReadWorkflowEventsAsync(
        string projectId,
        string runId,
        ulong afterSequence,
        int limit,
        CancellationToken ct = default);

    Task<Qyl.Api.Contracts.Workflow.WorkflowGraphSnapshot?> GetWorkflowGraphAsync(
        string projectId,
        string runId,
        string? nodeCursor,
        int nodeLimit,
        string? edgeCursor,
        int edgeLimit,
        CancellationToken ct = default);

    Task RebuildWorkflowProjectionAsync(
        string projectId,
        string runId,
        CancellationToken ct = default);

    Task<WorkflowContentReadRow?> GetWorkflowContentAsync(
        string projectId,
        string runId,
        string contentRef,
        CancellationToken ct = default);

    Task<WorkflowControlCommandStorageRow?> SubmitWorkflowControlAsync(
        string projectId,
        string runId,
        Qyl.Api.Contracts.Workflow.WorkflowControlAction action,
        string idempotencyKey,
        string? input,
        DateTimeOffset requestedAt,
        CancellationToken ct = default);

    Task<WorkflowControlCommandStoragePage?> PollWorkflowControlsAsync(
        string projectId,
        string runId,
        ulong afterSequence,
        int limit,
        CancellationToken ct = default);

    Task<WorkflowControlCommandStorageRow?> UpdateWorkflowControlAsync(
        string projectId,
        string runId,
        string commandId,
        Qyl.Api.Contracts.Workflow.WorkflowControlStatus status,
        string? error,
        DateTimeOffset updatedAt,
        CancellationToken ct = default);

    Task<WorkflowRetentionResult> DeleteExpiredWorkflowDataBatchAsync(
        DateTimeOffset cutoff,
        int batchSize,
        CancellationToken ct = default);

}
