namespace qyl.mcp.Tools;

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
internal class TelemetryJsonContext : JsonSerializerContext;
