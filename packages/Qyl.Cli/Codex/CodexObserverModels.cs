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
