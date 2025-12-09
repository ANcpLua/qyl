using System.Text.Json.Serialization;

namespace qyl.mcp.server.Tools;

/// <summary>
/// JSON serialization context for AOT compatibility with telemetry types.
/// </summary>
[JsonSerializable(typeof(AgentRun))]
[JsonSerializable(typeof(AgentRun[]))]
[JsonSerializable(typeof(TokenUsageSummary))]
[JsonSerializable(typeof(TokenUsageSummary[]))]
[JsonSerializable(typeof(AgentError))]
[JsonSerializable(typeof(AgentError[]))]
[JsonSerializable(typeof(LatencyStats))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal partial class TelemetryJsonContext : JsonSerializerContext;