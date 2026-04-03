using Qyl.Contracts.Agenting;

namespace Qyl.Collector.Autofix.V2;

public static class AutofixLedgerProjector
{
    public static AutofixRunProjection ProjectRun(AutofixRunLedgerRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var state = record.State;
        var pendingApprovalCount = record.ApprovalRequests.Count(request =>
            record.ApprovalDecisions.All(decision =>
                !string.Equals(decision.ApprovalId, request.ApprovalId, StringComparison.Ordinal) ||
                decision.Status == AgentRunApprovalStatus.Pending));

        return new AutofixRunProjection(
            state.RunId,
            state.IssueId,
            state.Phase,
            state.Status,
            state.Attempt,
            state.MaxAttempts,
            state.LatestConfidence,
            pendingApprovalCount,
            record.ArtifactRefs.Count,
            record.ValidationCheckpoints.Count,
            state.UpdatedAtUtc);
    }

    public static AutofixArtifactProjection ProjectArtifact(
        AgentRunArtifactRef artifactRef,
        AgentRunArtifact? artifact = null)
    {
        ArgumentNullException.ThrowIfNull(artifactRef);

        return new AutofixArtifactProjection(
            artifactRef.ArtifactId,
            artifactRef.RunId,
            artifactRef.Kind,
            artifact?.Name ?? artifactRef.Summary ?? artifactRef.ArtifactId,
            artifactRef.Locator,
            artifactRef.ContentType,
            artifact?.MimeType ?? artifactRef.ContentType,
            artifactRef.ProducedAtUtc,
            artifact?.SourceTool,
            artifact?.SourceStep);
    }

    public static AutofixApprovalProjection ProjectApproval(
        AgentRunApprovalRequest request,
        AgentRunApprovalDecision? decision)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new AutofixApprovalProjection(
            request.ApprovalId,
            request.RunId,
            request.Phase,
            request.CapabilityId,
            decision?.Status ?? AgentRunApprovalStatus.Pending,
            request.RequestedBy,
            request.RequestedFor,
            request.RequestedAtUtc,
            decision?.DecidedAtUtc,
            request.ArtifactId);
    }

    public static AutofixValidationProjection ProjectValidation(ValidationCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);

        return new AutofixValidationProjection(
            checkpoint.CheckpointId,
            checkpoint.RunId,
            checkpoint.Name,
            checkpoint.Outcome,
            checkpoint.Confidence,
            checkpoint.EvaluatedAtUtc,
            checkpoint.FailureReason);
    }
}
