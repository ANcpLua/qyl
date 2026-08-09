using System.Text.Json.Serialization;

namespace Qyl.Cli.Codex;

[AttributeUsage(AttributeTargets.Class)]
internal sealed class LocalJsonStateContextAttribute : Attribute;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
[JsonSerializable(typeof(Qyl.Api.Contracts.Workflow.WorkflowRunCreateRequest))]
[JsonSerializable(typeof(Qyl.Api.Contracts.Workflow.WorkflowEventAppend))]
[JsonSerializable(typeof(Qyl.Api.Contracts.Workflow.WorkflowEventAppend[]))]
[JsonSerializable(typeof(Qyl.Api.Contracts.Workflow.WorkflowContentChunk))]
[JsonSerializable(typeof(Qyl.Api.Contracts.Workflow.WorkflowContentChunk[]))]
[JsonSerializable(typeof(Qyl.Api.Contracts.Workflow.WorkflowEventBatchAppendRequest))]
[JsonSerializable(typeof(Qyl.Api.Contracts.Workflow.WorkflowEventBatchAppendResponse))]
[JsonSerializable(typeof(Qyl.Api.Contracts.Workflow.WorkflowControlCommand))]
[JsonSerializable(typeof(Qyl.Api.Contracts.Workflow.WorkflowControlCommand[]))]
[JsonSerializable(typeof(Qyl.Api.Contracts.Workflow.WorkflowControlCommandPage))]
[JsonSerializable(typeof(Qyl.Api.Contracts.Workflow.WorkflowControlStatusUpdateRequest))]
internal partial class CodexWorkflowContractJsonContext : JsonSerializerContext;

[LocalJsonStateContext]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
[JsonSerializable(typeof(WorkflowSpoolMetadata))]
[JsonSerializable(typeof(WorkflowSpoolEntry))]
[JsonSerializable(typeof(WorkflowSpoolEntry[]))]
[JsonSerializable(typeof(WorkflowSpoolEnvelope))]
[JsonSerializable(typeof(ActiveWorkflowRun))]
[JsonSerializable(typeof(DiagnosticSnapshotInboxRequest))]
[JsonSerializable(typeof(DiagnosticSnapshotInboxAcknowledgement))]
[JsonSerializable(typeof(string))]
internal partial class CodexObserverStateJsonContext : JsonSerializerContext;
