// =============================================================================
// qyl.copilot - Source-Generated JSON Context
// AOT-compatible JSON serialization for copilot types
// =============================================================================

using System.Text.Json.Serialization;
using qyl.protocol.Copilot;

namespace qyl.copilot;

/// <summary>
///     Source-generated JSON serializer context for AOT compatibility.
///     All types serialized in this component must be registered here.
/// </summary>
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(StreamUpdate))]
[JsonSerializable(typeof(StreamUpdate[]))]
[JsonSerializable(typeof(CopilotWorkflow))]
[JsonSerializable(typeof(CopilotWorkflow[]))]
[JsonSerializable(typeof(WorkflowExecution))]
[JsonSerializable(typeof(WorkflowExecution[]))]
[JsonSerializable(typeof(ChatMessage))]
[JsonSerializable(typeof(ChatMessage[]))]
[JsonSerializable(typeof(CopilotContext))]
[JsonSerializable(typeof(CopilotAuthStatus))]
[JsonSerializable(typeof(ChatRequest))]
[JsonSerializable(typeof(WorkflowRunRequest))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class CopilotJsonContext : JsonSerializerContext;
