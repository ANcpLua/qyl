using Qyl.Contracts.Agenting;

namespace Qyl.Collector.Autofix.V2;

public interface IAutofixRunLedger
{
    ValueTask<AutofixRunLedgerRecord?> GetAsync(string runId, CancellationToken cancellationToken = default);

    ValueTask UpsertStateAsync(AutofixRunState state, CancellationToken cancellationToken = default);

    ValueTask AppendEventAsync(RunLedgerEvent runEvent, CancellationToken cancellationToken = default);

    ValueTask AppendArtifactAsync(
        AgentRunArtifact artifact,
        AgentRunArtifactRef artifactRef,
        CancellationToken cancellationToken = default);

    ValueTask AppendApprovalAsync(
        AgentRunApprovalRequest request,
        AgentRunApprovalDecision? decision,
        CancellationToken cancellationToken = default);

    ValueTask AppendPolicyEvaluationAsync(
        AgentRunPolicyEvaluation evaluation,
        CancellationToken cancellationToken = default);

    ValueTask AppendValidationAsync(
        ValidationCheckpoint checkpoint,
        ConfidenceSignal? signal,
        CancellationToken cancellationToken = default);
}

public sealed record AutofixRunLedgerRecord(
    AutofixRunState State,
    IReadOnlyList<RunLedgerEvent> Events,
    IReadOnlyList<AgentRunArtifactRef> ArtifactRefs,
    IReadOnlyList<AgentRunArtifact> Artifacts,
    IReadOnlyList<AgentRunApprovalRequest> ApprovalRequests,
    IReadOnlyList<AgentRunApprovalDecision> ApprovalDecisions,
    IReadOnlyList<AgentRunPolicyEvaluation> PolicyEvaluations,
    IReadOnlyList<ValidationCheckpoint> ValidationCheckpoints,
    IReadOnlyList<ConfidenceSignal> ConfidenceSignals);
