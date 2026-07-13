namespace Qyl.Collector.Storage;

internal sealed class QylStoreUnavailableException(string message) : Exception(message);

internal interface IQylStore : IAsyncDisposable
{
    ValueTask EnqueueAsync(SpanBatch batch, CancellationToken ct = default);

    Task InsertLogsAsync(IReadOnlyList<LogStorageRow> logs, CancellationToken ct = default);

    Task InsertProfilesAsync(IReadOnlyList<ProfileDetail> results, CancellationToken ct = default);

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

    Task<StorageStats> GetStorageStatsAsync(string projectId, CancellationToken ct = default);

    Task<long> GetModelPricingCountAsync(CancellationToken ct = default);

    Task<IReadOnlyList<ModelPricingRow>> GetActiveModelPricingAsync(CancellationToken ct = default);

    Task InsertModelPricingSeedsAsync(
        IReadOnlyList<ModelPricingRow> entries,
        DateTimeOffset validFrom,
        CancellationToken ct = default);

    Task<IReadOnlyList<LogStorageRow>> GetLogsAsync(
        string projectId,
        string? sessionId = null,
        string? traceId = null,
        string? severityText = null,
        int? minSeverity = null,
        string? search = null,
        ulong? start = null,
        ulong? after = null,
        string? afterLogId = null,
        ulong? before = null,
        string? serviceName = null,
        bool ascending = false,
        bool latestPageAscending = false,
        int limit = 500,
        CancellationToken ct = default);

    Task<IReadOnlyList<ProfileStorageRow>> GetProfilesAsync(
        string projectId,
        string? sessionId = null,
        string? traceId = null,
        string? spanId = null,
        string? serviceName = null,
        string? sampleType = null,
        int limit = 100,
        CancellationToken ct = default);

    Task<ProfileDetail?> GetProfileDetailAsync(
        string profileId,
        string projectId,
        CancellationToken ct = default);
}
