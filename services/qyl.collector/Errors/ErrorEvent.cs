namespace Qyl.Collector.Errors;

internal sealed record ErrorEvent
{
    public required string ErrorType { get; init; }
    public required string Message { get; init; }
    public required string Category { get; init; }
    public required string Fingerprint { get; init; }
    public required string ServiceName { get; init; }
    public required string TraceId { get; init; }
    public string? UserId { get; init; }
    public string? GenAiProvider { get; init; }
    public string? GenAiModel { get; init; }
    public string? GenAiOperation { get; init; }
    public string? GenAiFinishReasons { get; init; }
    public string? GenAiToolName { get; init; }
    public long? GenAiInputTokens { get; init; }
    public long? GenAiOutputTokens { get; init; }
    public string? GenAiAgentName { get; init; }
    public string? GenAiAgentId { get; init; }
}
