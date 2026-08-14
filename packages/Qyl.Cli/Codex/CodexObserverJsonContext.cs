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
[JsonSerializable(typeof(Qyl.Api.Contracts.Diagnostics.AgentDiagnosticSnapshot))]
[JsonSerializable(typeof(Qyl.Api.Contracts.Diagnostics.AgentDiagnosticSnapshotSummary))]
[JsonSerializable(typeof(Qyl.Api.Contracts.Diagnostics.AgentDiagnosticVariable))]
[JsonSerializable(typeof(Qyl.Api.Contracts.Diagnostics.CapturedAgentDiagnosticVariable))]
[JsonSerializable(typeof(Qyl.Api.Contracts.Diagnostics.RedactedAgentDiagnosticVariable))]
[JsonSerializable(typeof(Qyl.Api.Contracts.Diagnostics.OmittedAgentDiagnosticVariable))]
[JsonSerializable(typeof(Qyl.Api.Contracts.Diagnostics.AgentDiagnosticCheckResult))]
[JsonSerializable(typeof(Qyl.Api.Contracts.Mcp.Tools.GetActiveWorkflowRunInput))]
[JsonSerializable(typeof(Qyl.Api.Contracts.Mcp.Tools.GetActiveWorkflowRunOutput))]
[JsonSerializable(typeof(Qyl.Api.Contracts.Mcp.Tools.RecordDiagnosticSnapshotInput))]
[JsonSerializable(typeof(Qyl.Api.Contracts.Mcp.Tools.RecordDiagnosticSnapshotOutput))]
[JsonSerializable(typeof(Qyl.Api.Contracts.Mcp.Tools.RecordDiagnosticSnapshotVariableInput))]
[JsonSerializable(typeof(Qyl.Api.Contracts.Mcp.Tools.RecordDiagnosticSnapshotCheckInput))]
[JsonSerializable(typeof(System.Text.Json.JsonElement))]
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
[JsonSerializable(typeof(long))]
internal partial class CodexObserverStateJsonContext : JsonSerializerContext;
