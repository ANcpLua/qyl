// Copyright (c) 2025-2026 ancplua

namespace Qyl.Loom.Patterns.Contracts;

/// <summary>
///     Stage-1 output — root-cause analysis of a captured signal.
///     Consumed by the solution stage and by the final confidence verdict.
/// </summary>
public sealed record RootCauseHypothesis(
    string SignalId,
    string Hypothesis,
    double Confidence);

/// <summary>
///     Stage-2 output — the proposed fix laid out as discrete actionable steps.
/// </summary>
public sealed record SolutionPlan(
    string SignalId,
    string Approach,
    IReadOnlyList<string> Steps);

/// <summary>
///     Stage-3 output — final verdict. <see cref="Approved" /> drives the
///     <c>ForwardMessage</c> fan-out in <c>Pattern06_AllCombined</c>.
/// </summary>
public sealed record ConfidenceVerdict(
    string SignalId,
    bool Approved,
    string Reason);

/// <summary>
///     The entry message for every pattern — identifies which captured signal is being
///     investigated and carries enough context to synthesize the fake-agent responses.
/// </summary>
public sealed record IncidentSignal(
    string Id,
    string Service,
    string Severity,
    string Description);

/// <summary>Per-run state used by <c>Pattern06_AllCombined</c>'s stateful intake.</summary>
public sealed record AutofixCombinedState(int SignalsSeen);
