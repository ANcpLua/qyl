using Qyl.Api.Contracts.Workflow;

namespace Qyl.Cli.Codex;

internal sealed record WorkflowSpoolMetadata(
    string RunId,
    string? ThreadId,
    string? Title,
    DateTimeOffset StartedAt,
    string CodexVersion,
    string SchemaDigest,
    string WorkingDirectory,
    bool Sealed);

internal sealed record WorkflowSpoolEntry(
    WorkflowEventAppend Event,
    IReadOnlyList<WorkflowContentChunk> Content);

internal sealed record WorkflowSpoolEnvelope(
    string Nonce,
    string Tag,
    string Ciphertext);

internal sealed record ActiveWorkflowRun(
    string RunId,
    string? ThreadId,
    DateTimeOffset StartedAt,
    int ProcessId);

internal sealed record DiagnosticSnapshotInboxRequest(
    string RunId,
    string SnapshotId,
    string ProbeId,
    string Phase,
    string Outcome,
    int VariableCount,
    int CheckCount,
    int FailedCheckCount,
    string PayloadDigest,
    DateTimeOffset SubmittedAt,
    Qyl.Api.Contracts.Workflow.WorkflowContentChunk Content);

internal sealed record DiagnosticSnapshotInboxAcknowledgement(
    string RunId,
    string SnapshotId,
    string PayloadDigest,
    string Status,
    string Code,
    string? EventId);

internal readonly record struct DiagnosticSnapshotSubmissionResult(
    bool Recorded,
    string Code,
    string SnapshotId,
    string? EventId);

internal sealed record CodexSchemaIdentity(
    string CodexVersion,
    string SchemaDirectory,
    string SchemaDigest);

internal readonly record struct CodexControlTarget(
    string? ThreadId,
    string? TurnId);

internal readonly record struct CodexNormalizedBatch(
    IReadOnlyList<WorkflowEventAppend> Events,
    IReadOnlyList<WorkflowContentChunk> Content);
