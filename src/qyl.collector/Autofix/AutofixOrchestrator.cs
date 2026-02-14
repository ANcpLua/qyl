using qyl.collector.Errors;

namespace qyl.collector.Autofix;

/// <summary>
///     Orchestrates agent-driven fix runs for grouped errors. Creates workflow
///     executions, assembles fix context, and tracks run status through its lifecycle.
/// </summary>
public sealed partial class AutofixOrchestrator(DuckDbStore store, ILogger<AutofixOrchestrator> logger)
{
    internal DuckDbStore Store => store;

    /// <summary>
    ///     Creates a new fix run linked to the specified issue, setting up initial
    ///     fix context from the error details, stack trace, and affected files.
    /// </summary>
    public async Task<FixRunRecord> CreateFixRunAsync(
        string issueId, IssueSummary issue, FixPolicy policy, CancellationToken ct = default)
    {
        var runId = Guid.NewGuid().ToString("N");
        var executionId = Guid.NewGuid().ToString("N");

        // Create the linked workflow execution
        var execution = new WorkflowExecutionRecord
        {
            ExecutionId = executionId,
            WorkflowName = "autofix",
            Trigger = "fix_run",
            Status = "pending",
            InputJson = JsonSerializer.Serialize(new
            {
                issueId,
                errorType = issue.ErrorType,
                errorMessage = issue.ErrorMessage,
                fingerprint = issue.Fingerprint
            }),
            StartTimeUnixNano = TimeProvider.System.GetUtcNow().ToUnixTimeMilliseconds() * 1_000_000L
        };

        await store.InsertWorkflowExecutionAsync(execution, ct).ConfigureAwait(false);

        var record = new FixRunRecord
        {
            RunId = runId,
            IssueId = issueId,
            ExecutionId = executionId,
            Status = "pending",
            Policy = policy.ToString().ToLowerInvariant()
        };

        await store.InsertFixRunAsync(record, ct).ConfigureAwait(false);

        LogFixRunCreated(runId, issueId, policy);
        return record;
    }

    /// <summary>
    ///     Updates the status of a fix run and applies the policy gate.
    /// </summary>
    public async Task UpdateFixRunStatusAsync(
        string runId, string status, string? description = null,
        double? confidence = null, string? changesJson = null, CancellationToken ct = default)
    {
        await store.UpdateFixRunAsync(runId, status, description, confidence, changesJson, ct)
            .ConfigureAwait(false);
        LogFixRunUpdated(runId, status);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Fix run {RunId} created for issue {IssueId} with policy {Policy}")]
    private partial void LogFixRunCreated(string runId, string issueId, FixPolicy policy);

    [LoggerMessage(Level = LogLevel.Information, Message = "Fix run {RunId} status updated to {Status}")]
    private partial void LogFixRunUpdated(string runId, string status);
}
