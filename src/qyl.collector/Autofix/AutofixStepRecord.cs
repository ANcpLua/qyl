namespace qyl.collector.Autofix;

/// <summary>
///     Storage record for an individual step within a fix run.
///     Maps to the autofix_steps DuckDB table.
/// </summary>
public sealed record AutofixStepRecord
{
    public required string StepId { get; init; }
    public required string RunId { get; init; }
    public required int StepNumber { get; init; }
    public required string StepName { get; init; }
    public required string Status { get; init; }
    public string? InputJson { get; init; }
    public string? OutputJson { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime? CreatedAt { get; init; }
}
