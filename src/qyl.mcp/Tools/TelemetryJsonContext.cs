namespace qyl.mcp.Tools;

using System.Text.Json.Serialization;

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
