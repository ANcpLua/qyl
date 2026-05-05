namespace Qyl.Collector.Autofix;

[QylService(QylLifetime.Singleton)]
public sealed partial class AgentHandoffService(
    DuckDbStore store,
    IConfiguration configuration,
    ILogger<AgentHandoffService> logger)
{
    private readonly int _timeoutMinutes = configuration.GetValue("QYL_HANDOFF_TIMEOUT_MINUTES", 30);

    public async Task<AgentHandoffRecord> CreateHandoffAsync(
        string runId, string agentType, CancellationToken ct)
    {
        var fixRun = await store.GetFixRunAsync(runId, ct).ConfigureAwait(false)
                     ?? throw new InvalidOperationException($"Fix run '{runId}' not found");

        var steps = await store.GetAutofixStepsAsync(runId, ct).ConfigureAwait(false);

        HandoffContext context = new(
            fixRun.RunId,
            fixRun.IssueId,
            fixRun.FixDescription,
            fixRun.ConfidenceScore,
            fixRun.ChangesJson,
            steps.Select(static s => new HandoffStepSummary(s.StepName, s.OutputJson)).ToList());

        var contextJson = JsonSerializer.Serialize(context, HandoffJsonContext.Default.HandoffContext);

        var handoffId = Guid.NewGuid().ToString("N");
        var timeoutAt = TimeProvider.System.GetUtcNow().UtcDateTime.AddMinutes(_timeoutMinutes);

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

    public async Task<AgentHandoffRecord?> AcceptHandoffAsync(string handoffId, CancellationToken ct)
    {
        var affected = await store.UpdateHandoffStatusAsync(
            handoffId, "accepted", expectedCurrentStatus: "pending", ct: ct).ConfigureAwait(false);

        if (affected is 0)
            return null;

        LogHandoffAccepted(handoffId);
        return await store.GetHandoffAsync(handoffId, ct).ConfigureAwait(false);
    }

    public async Task<AgentHandoffRecord?> SubmitHandoffResultAsync(
        string handoffId, string resultJson, CancellationToken ct)
    {
        var handoff = await store.GetHandoffAsync(handoffId, ct).ConfigureAwait(false);
        if (handoff is null || handoff.Status is not "accepted")
            return null;

        await store.UpdateHandoffStatusAsync(handoffId, "completed", resultJson, ct: ct)
            .ConfigureAwait(false);

        LogHandoffSubmitted(handoffId);
        return await store.GetHandoffAsync(handoffId, ct).ConfigureAwait(false);
    }

    public async Task<AgentHandoffRecord?> FailHandoffAsync(
        string handoffId, string errorMessage, CancellationToken ct)
    {
        var handoff = await store.GetHandoffAsync(handoffId, ct).ConfigureAwait(false);
        if (handoff is null || handoff.Status is not ("pending" or "accepted"))
            return null;

        await store.UpdateHandoffStatusAsync(handoffId, "failed", errorMessage: errorMessage, ct: ct)
            .ConfigureAwait(false);

        LogHandoffFailed(handoffId, errorMessage);
        return await store.GetHandoffAsync(handoffId, ct).ConfigureAwait(false);
    }

    public async Task<string?> GetHandoffContextAsync(string handoffId, CancellationToken ct)
    {
        var handoff = await store.GetHandoffAsync(handoffId, ct).ConfigureAwait(false);
        return handoff?.ContextJson;
    }


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

public sealed record HandoffContext(
    string RunId,
    string IssueId,
    string? FixDescription,
    double? Confidence,
    string? ChangesJson,
    IReadOnlyList<HandoffStepSummary> Steps);

public sealed record HandoffStepSummary(string StepName, string? OutputJson);

[JsonSerializable(typeof(HandoffContext))]
public sealed partial class HandoffJsonContext : JsonSerializerContext;
