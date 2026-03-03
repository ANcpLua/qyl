namespace qyl.collector.Autofix;

/// <summary>
///     Manages the lifecycle of coding agent handoffs: creating handoff contexts
///     from fix runs, accepting/completing/failing handoffs, and retrieving context
///     for agents to work with.
/// </summary>
public sealed partial class AgentHandoffService(
    DuckDbStore store,
    IConfiguration configuration,
    ILogger<AgentHandoffService> logger)
{
    private readonly int _timeoutMinutes = configuration.GetValue("QYL_HANDOFF_TIMEOUT_MINUTES", 30);

    /// <summary>
    ///     Creates a new handoff from a fix run, assembling the full RCA + solution plan
    ///     context for the coding agent.
    /// </summary>
    public async Task<AgentHandoffRecord> CreateHandoffAsync(
        string runId, string agentType, CancellationToken ct)
    {
        FixRunRecord fixRun = await store.GetFixRunAsync(runId, ct).ConfigureAwait(false)
                              ?? throw new InvalidOperationException($"Fix run '{runId}' not found");

        IReadOnlyList<AutofixStepRecord> steps = await store.GetAutofixStepsAsync(runId, ct).ConfigureAwait(false);

        HandoffContext context = new(
            RunId: fixRun.RunId,
            IssueId: fixRun.IssueId,
            FixDescription: fixRun.FixDescription,
            Confidence: fixRun.ConfidenceScore,
            ChangesJson: fixRun.ChangesJson,
            Steps: steps.Select(static s => new HandoffStepSummary(s.StepName, s.OutputJson)).ToList());

        string contextJson = JsonSerializer.Serialize(context, HandoffJsonContext.Default.HandoffContext);

        string handoffId = Guid.NewGuid().ToString("N");
        DateTime timeoutAt = TimeProvider.System.GetUtcNow().UtcDateTime.AddMinutes(_timeoutMinutes);

        AgentHandoffRecord record = new()
        {
            HandoffId = handoffId,
            RunId = runId,
            AgentType = agentType,
            Status = "pending",
            ContextJson = contextJson,
            TimeoutAt = timeoutAt
        };

        await store.InsertHandoffAsync(record, ct).ConfigureAwait(false);

        LogHandoffCreated(handoffId, runId, agentType);
        return record;
    }

    /// <summary>
    ///     Marks a pending handoff as accepted by an agent.
    /// </summary>
    public async Task<AgentHandoffRecord?> AcceptHandoffAsync(string handoffId, CancellationToken ct)
    {
        AgentHandoffRecord? handoff = await store.GetHandoffAsync(handoffId, ct).ConfigureAwait(false);
        if (handoff is null || handoff.Status is not "pending")
            return null;

        await store.UpdateHandoffStatusAsync(handoffId, "accepted", ct: ct).ConfigureAwait(false);

        LogHandoffAccepted(handoffId);
        return await store.GetHandoffAsync(handoffId, ct).ConfigureAwait(false);
    }

    /// <summary>
    ///     Submits the result of a completed handoff.
    /// </summary>
    public async Task<AgentHandoffRecord?> SubmitHandoffResultAsync(
        string handoffId, string resultJson, CancellationToken ct)
    {
        AgentHandoffRecord? handoff = await store.GetHandoffAsync(handoffId, ct).ConfigureAwait(false);
        if (handoff is null || handoff.Status is not "accepted")
            return null;

        await store.UpdateHandoffStatusAsync(handoffId, "completed", resultJson: resultJson, ct: ct)
            .ConfigureAwait(false);

        LogHandoffSubmitted(handoffId);
        return await store.GetHandoffAsync(handoffId, ct).ConfigureAwait(false);
    }

    /// <summary>
    ///     Marks a pending or accepted handoff as failed.
    /// </summary>
    public async Task<AgentHandoffRecord?> FailHandoffAsync(
        string handoffId, string errorMessage, CancellationToken ct)
    {
        AgentHandoffRecord? handoff = await store.GetHandoffAsync(handoffId, ct).ConfigureAwait(false);
        if (handoff is null || handoff.Status is not ("pending" or "accepted"))
            return null;

        await store.UpdateHandoffStatusAsync(handoffId, "failed", errorMessage: errorMessage, ct: ct)
            .ConfigureAwait(false);

        LogHandoffFailed(handoffId, errorMessage);
        return await store.GetHandoffAsync(handoffId, ct).ConfigureAwait(false);
    }

    /// <summary>
    ///     Returns the context JSON for a handoff (the full RCA + plan for the agent).
    /// </summary>
    public async Task<string?> GetHandoffContextAsync(string handoffId, CancellationToken ct)
    {
        AgentHandoffRecord? handoff = await store.GetHandoffAsync(handoffId, ct).ConfigureAwait(false);
        return handoff?.ContextJson;
    }

    // ── LoggerMessage source-generated log methods ──────────────────────────

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Handoff {HandoffId} created for run {RunId} with agent type {AgentType}")]
    private partial void LogHandoffCreated(string handoffId, string runId, string agentType);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Handoff {HandoffId} accepted by agent")]
    private partial void LogHandoffAccepted(string handoffId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Handoff {HandoffId} result submitted")]
    private partial void LogHandoffSubmitted(string handoffId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Handoff {HandoffId} failed: {Error}")]
    private partial void LogHandoffFailed(string handoffId, string error);
}

/// <summary>
///     Assembled context passed to a coding agent for a handoff, including
///     the fix run details and all autofix step outputs.
/// </summary>
internal sealed record HandoffContext(
    string RunId,
    string IssueId,
    string? FixDescription,
    double? Confidence,
    string? ChangesJson,
    IReadOnlyList<HandoffStepSummary> Steps);

/// <summary>
///     Summary of a single autofix step included in the handoff context.
/// </summary>
internal sealed record HandoffStepSummary(string StepName, string? OutputJson);

[JsonSerializable(typeof(HandoffContext))]
internal sealed partial class HandoffJsonContext : JsonSerializerContext;
