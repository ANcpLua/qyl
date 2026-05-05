namespace Qyl.Collector.Provisioning;

[QylService(QylLifetime.Singleton)]
public sealed partial class GenerationJobService(DuckDbStore store, ILogger<GenerationJobService> logger)
{
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

    public Task<GenerationJobRecord?> GetJobAsync(
        string jobId,
        CancellationToken ct = default) =>
        store.GetGenerationJobAsync(jobId, ct);


    [LoggerMessage(Level = LogLevel.Information,
        Message = "Generation job created: {JobId} for profile {ProfileId}")]
    private partial void LogJobCreated(string jobId, string profileId);
}
