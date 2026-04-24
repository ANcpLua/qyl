// Copyright (c) 2025-2026 ancplua

using System.ComponentModel;

namespace Qyl.Loom.Autofix;

/// <summary>
///     Structured output of a single <see cref="LoomAutofixRunner" /> pass. Passed into
///     <c>agent.RunAsync&lt;AutofixReport&gt;(...)</c> with
///     <c>ChatResponseFormat.ForJsonSchema&lt;AutofixReport&gt;()</c> so MAF enforces the
///     shape at the chat-completions layer — no manual XML or JSON parsing, no fallback paths.
/// </summary>
public sealed record AutofixReport
{
    [Description(
        "Fixability score out of 5. Sum of 5 binary inputs: stack_trace resolves, trace linked, " +
        "breadcrumbs present in last 60s, stack trace references files that exist in the repo, " +
        "error is deterministic (hash seen more than once). Score >= 3 -> continue, < 3 -> stop.")]
    public required int FixabilityScore { get; init; }

    [Description("Either 'continue' (score >= 3) or 'need_more_signal' (score < 3). No other values.")]
    public required string FixabilityDecision { get; init; }

    [Description(
        "Specific telemetry that would push the fixability score to >= 3. Null when " +
        "FixabilityDecision is 'continue'.")]
    public string? MissingSignal { get; init; }

    [Description(
        "Short summary of the correlated signals gathered from qyl telemetry AND the signals " +
        "searched for but absent. Absence is evidence.")]
    public required string ContextSummary { get; init; }

    [Description(
        "Primary root-cause hypothesis. Names a MECHANISM, not a symptom. Each claim includes the " +
        "tool/signal it cites. Filled when FixabilityDecision is 'continue'; otherwise a short " +
        "statement of why no hypothesis is possible.")]
    public required string PrimaryHypothesis { get; init; }

    [Description("Optional secondary hypothesis, ranked below the primary. Null when unranked or not applicable.")]
    public string? AlternativeHypothesis { get; init; }

    [Description(
        "Repo name (owner/repo or the local qyl repo key) for the Solution diff. Null when no " +
        "patch was produced (fixability gate, need_more_signal, cross-repo unresolved).")]
    public string? SolutionRepo { get; init; }

    [Description(
        "Unified diff applying the minimal root-cause fix. No adjacent refactors. Mirror neighbour " +
        "file style. Preserve existing tests (flag changes, do not rewrite). Null when no patch.")]
    public string? SolutionDiff { get; init; }

    [Description(
        "Single regression test named for the issue id. Synthetic inputs only — never reproduce " +
        "event-payload values verbatim. Fails pre-patch, passes post-patch. Null when no patch.")]
    public string? RegressionTest { get; init; }

    [Description(
        "Confidence level: 'high' when the four gates sum >= 9/12, 'medium' for 6-8, 'low' for < 6. " +
        "'low' implies a require_review policy regardless of the caller's requested policy.")]
    public required string ConfidenceLevel { get; init; }

    [Description("Sum of the four confidence gates: evidence + regression + completeness + self_challenge. Range 0-12.")]
    public required int ConfidenceScoreSum { get; init; }

    [Description("Evidence gate score (0-3). Higher when the hypothesis cites multiple sources across telemetry layers.")]
    public required int EvidenceGate { get; init; }

    [Description("Regression gate score (0-3). 3 when the test fails pre-patch AND passes post-patch.")]
    public required int RegressionGate { get; init; }

    [Description("Completeness gate score (0-3). 3 when the diff fixes root cause with no TODOs or scope-punts.")]
    public required int CompletenessGate { get; init; }

    [Description("Self-challenge gate score (0-3). 3 when the agent argued against its own fix and addressed the strongest counter.")]
    public required int SelfChallengeGate { get; init; }

    [Description(
        "Plain-English summary for the human reviewer. 200 words max. No meta-commentary. No " +
        "'let me know if...'. This is the handoff.")]
    public required string FinalReport { get; init; }
}
