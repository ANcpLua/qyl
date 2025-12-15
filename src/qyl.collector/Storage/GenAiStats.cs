namespace qyl.collector.Storage;

public sealed record GenAiStats
{
    public long RequestCount { get; init; }
    public long TotalInputTokens { get; init; }
    public long TotalOutputTokens { get; init; }
    public decimal TotalCostUsd { get; init; }
    public float? AverageEvalScore { get; init; }
}
