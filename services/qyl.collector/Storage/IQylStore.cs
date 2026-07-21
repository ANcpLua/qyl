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

}
