namespace qyl.collector.Provisioning;

/// <summary>
///     Manages code generation jobs for instrumentation profile selections.
/// </summary>
public sealed partial class GenerationJobService(DuckDbStore store, ILogger<GenerationJobService> logger)
{
    /// <summary>
    ///     Creates a new generation job and returns its tracking record.
    /// </summary>
    public async Task<GenerationJobRecord> CreateJobAsync(
        GenerationJobRequest request,
        CancellationToken ct = default)
    {
        var jobId = $"gen-{Guid.CreateVersion7():N}"[..24];

        var job = new GenerationJobRecord(
            jobId,
            request.WorkspaceId,
            request.ProfileId,
            "pending",
            null,
            null,
            TimeProvider.System.GetUtcNow().UtcDateTime,
            null);

        await store.InsertGenerationJobAsync(job, ct).ConfigureAwait(false);

        LogJobCreated(jobId, request.ProfileId);

        return job;
    }

    /// <summary>
    ///     Gets the current status of a generation job.
    /// </summary>
    public Task<GenerationJobRecord?> GetJobAsync(
        string jobId,
        CancellationToken ct = default) =>
        store.GetGenerationJobAsync(jobId, ct);

    // ==========================================================================
    // LoggerMessage — structured, zero-allocation logging
    // ==========================================================================

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Generation job created: {JobId} for profile {ProfileId}")]
    private partial void LogJobCreated(string jobId, string profileId);
}
