namespace Qyl.Loom.Autofix;

public sealed partial class AutofixOrchestrator(CollectorClient collector, ILogger<AutofixOrchestrator> logger)
{
    public async Task<FixRunRecord> CreateFixRunAsync(
        string issueId, FixPolicy policy,
        string? instruction = null, string? stoppingPoint = null,
        CancellationToken ct = default)
    {
        var record = await collector.CreateFixRunAsync(issueId, policy, instruction, stoppingPoint, ct)
            .ConfigureAwait(false);
        LogFixRunCreated(record.RunId, issueId, policy);
        return record;
    }

    public async Task UpdateFixRunStatusAsync(
        string issueId, string runId, string status, string? description = null,
        double? confidence = null, string? changesJson = null, CancellationToken ct = default)
    {
        await collector.UpdateFixRunAsync(
                issueId, runId, status,
                description,
                confidence,
                changesJson,
                ct: ct)
            .ConfigureAwait(false);
        LogFixRunUpdated(runId, status);
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Fix run {RunId} created for issue {IssueId} with policy {Policy}")]
    private partial void LogFixRunCreated(string runId, string issueId, FixPolicy policy);

    [LoggerMessage(Level = LogLevel.Information, Message = "Fix run {RunId} status updated to {Status}")]
    private partial void LogFixRunUpdated(string runId, string status);
}
