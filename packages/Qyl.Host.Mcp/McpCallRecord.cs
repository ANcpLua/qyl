namespace Qyl.Host.Mcp;

/// <summary>
/// One completed passthrough call, as handed to <see cref="McpTelemetry.RecordCall"/> — the C#
/// shape of qyl.mcp's <c>McpCallSpanInput</c> (telemetry.ts).
/// </summary>
public sealed record McpCallRecord
{
    public required string Method { get; init; }
    public required string ServerName { get; init; }
    public required string Transport { get; init; }
    public string? ToolName { get; init; }
    public string? ResourceUri { get; init; }
    public IReadOnlyDictionary<string, string>? Arguments { get; init; }
    public string? ResultJson { get; init; }
    public int? ResultContentCount { get; init; }
    public string? Error { get; init; }
    public required DateTimeOffset StartTime { get; init; }
    public required DateTimeOffset EndTime { get; init; }
}
