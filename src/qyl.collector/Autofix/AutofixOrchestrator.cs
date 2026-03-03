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
            StartTimeUnixNano = TimeConversions.ToUnixNano(TimeProvider.System.GetUtcNow())
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

    /// <summary>
    ///     Launches a coding agent run for a completed fix run.
    /// </summary>
    public async Task<CodingAgent.CodingAgentRunRecord> LaunchCodingAgentAsync(
        string fixRunId, CodingAgent.CodingAgentProvider provider,
        string? repoFullName = null, CancellationToken ct = default)
    {
        var record = new CodingAgent.CodingAgentRunRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            FixRunId = fixRunId,
            Provider = provider.ToString().ToLowerInvariant(),
            Status = "pending",
            RepoFullName = repoFullName
        };

        await store.InsertCodingAgentRunAsync(record, ct).ConfigureAwait(false);
        LogCodingAgentLaunched(record.Id, fixRunId, provider);
        return record;
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Fix run {RunId} created for issue {IssueId} with policy {Policy}")]
    private partial void LogFixRunCreated(string runId, string issueId, FixPolicy policy);

    [LoggerMessage(Level = LogLevel.Information, Message = "Fix run {RunId} status updated to {Status}")]
    private partial void LogFixRunUpdated(string runId, string status);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Coding agent {AgentRunId} launched for fix run {FixRunId} with provider {Provider}")]
    private partial void LogCodingAgentLaunched(
        string agentRunId, string fixRunId, CodingAgent.CodingAgentProvider provider);
}
