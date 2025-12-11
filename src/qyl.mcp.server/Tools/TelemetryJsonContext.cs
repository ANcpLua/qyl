using System.Text.Json.Serialization;

namespace qyl.mcp.server.Tools;

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
