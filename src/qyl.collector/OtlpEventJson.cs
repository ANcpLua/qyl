namespace qyl.collector;

/// <summary>JSON-serializable OTLP event representation for AOT compatibility.</summary>
public sealed record OtlpEventJson(string? Name, ulong TimeUnixNano, Dictionary<string, string?>? Attributes);