// Copyright (c) 2025-2026 ancplua

namespace Qyl.Loom.Autofix.Workflow;

/// <summary>
///     Entry message for the autofix workflow. Carries just the run id; the first executor
///     loads the <see cref="FixRunRecord" /> and <see cref="IssueSummary" /> from the collector.
/// </summary>
public sealed record StartAutofix(string RunId);

/// <summary>
///     Immutable shared state threaded through every autofix pipeline executor.
///     Each executor returns a new instance via <c>with</c>-expressions as pipeline data accumulates.
/// </summary>
/// <remarks>
///     <para>
///         Early termination uses two orthogonal flags:
///         <see cref="IsEarlyStop" /> marks a requested stop at a <c>stopping_point</c> (maps to collector status
///         <c>review</c>), while <see cref="IsFatalError" /> marks an infrastructure failure (maps to <c>failed</c>).
///         When either flag is set, downstream pipeline executors short-circuit and forward the state unchanged to
///         the terminal <c>PolicyGateExecutor</c>, which is the single place that transitions the run to its final status.
///     </para>
/// </remarks>
public sealed record AutofixRunState
{
    public required string RunId { get; init; }
    public required string IssueId { get; init; }
    public required FixPolicy Policy { get; init; }
    public string? Instruction { get; init; }
    public string? StoppingPoint { get; init; }

    public IssueSummary? Issue { get; init; }
    public string? ContextJson { get; init; }
    public string? RcaReport { get; init; }
    public string? SolutionPlan { get; init; }
    public string? ChangesJson { get; init; }
    public ConfidenceResult? Confidence { get; init; }

    public bool IsEarlyStop { get; init; }
    public string? EarlyStopReason { get; init; }

    public bool IsFatalError { get; init; }
    public string? FatalErrorMessage { get; init; }
}
