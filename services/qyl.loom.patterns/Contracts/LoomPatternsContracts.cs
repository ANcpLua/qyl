// Copyright (c) 2025-2026 ancplua

using Qyl.Instrumentation.Instrumentation.Loom;

namespace Qyl.Loom.Patterns.Contracts;

/// <summary>
///     Stage-1 output — root-cause analysis of a captured signal.
///     Consumed by the solution stage and by the final confidence verdict.
/// </summary>
[LoomContract("loom.patterns.rca")]
public sealed partial record RootCauseHypothesis(
    string SignalId,
    string Hypothesis,
    double Confidence);

/// <summary>
///     Stage-2 output — the proposed fix laid out as discrete actionable steps.
/// </summary>
[LoomContract("loom.patterns.solution")]
public sealed partial record SolutionPlan(
    string SignalId,
    string Approach,
    IReadOnlyList<string> Steps);

/// <summary>
///     Stage-3 output — final verdict. <see cref="Approved" /> drives the
///     <c>ForwardMessage</c> fan-out in <c>Pattern06_AllCombined</c>.
/// </summary>
[LoomContract("loom.patterns.verdict")]
public sealed partial record ConfidenceVerdict(
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

/// <summary>
///     Per-run state used by <c>Pattern06_AllCombined</c>'s stateful intake.
///     Declared at top level because the Loom generator emits partials into a
///     synthetic namespace — nesting this record inside the pattern class would
///     collide with the container type.
/// </summary>
[LoomContract("loom.patterns.autofix.state")]
public sealed partial record AutofixCombinedState(int SignalsSeen);
