// Copyright (c) 2025-2026 ancplua

using System.ComponentModel;
using Qyl.Instrumentation.Instrumentation.Loom;

namespace Qyl.Loom.Autofix.Workflow;

/// <summary>
///     Runtime configuration for a single Autofix workflow execution. Passed in at the
///     workflow entry — every executor consults this for its tunables. No defaults baked
///     into executors; the orchestrator owns the policy.
/// </summary>
[LoomContract("loom.autofix.workflow.config")]
public sealed partial record AutofixWorkflowConfig(
    int HypothesisFanOut,
    double HypothesisTemperatureSpread,
    string? HypothesisAlternateModel,
    int ConfidenceRetryThreshold,
    int MaxConfidenceRetries,
    bool StoppingPointAfterHypothesis,
    bool StoppingPointBeforeCommit,
    bool ToolUsingContext,
    int ContextToolBudget,
    TimeSpan? StoppingPointTimeout);

/// <summary>Entry message — what the runner hands the workflow.</summary>
[LoomContract("loom.autofix.request")]
public sealed partial record AutofixWorkflowRequest(
    string RunId,
    string IssueId,
    string Policy,
    string? CallerInstruction,
    string? StoppingPoint,
    AutofixWorkflowConfig Config);

/// <summary>Stage 1 — fixability gate verdict.</summary>
[LoomContract("loom.autofix.fixability")]
public sealed partial record FixabilityVerdict(
    string RunId,
    [property: Description("0..5")] int Score,
    [property: Description("'continue' | 'need_more_signal'")] string Decision,
    string? MissingSignal);

/// <summary>Stage 2 — context-gathering output.</summary>
[LoomContract("loom.autofix.context")]
public sealed partial record ContextSummary(
    string RunId,
    string Summary,
    IReadOnlyList<string> SignalsFound,
    IReadOnlyList<string> SignalsAbsent);

/// <summary>Stage 3 — single hypothesis verdict (one per fan-out branch).</summary>
[LoomContract("loom.autofix.hypothesis")]
public sealed partial record HypothesisCandidate(
    string RunId,
    string BranchId,
    string Primary,
    string? Alternative,
    double SelfReportedConfidence);

/// <summary>Stage 3 — judge output, picks the winning hypothesis after fan-in.</summary>
[LoomContract("loom.autofix.hypothesis.judged")]
public sealed partial record HypothesisVerdict(
    string RunId,
    string Primary,
    string? Alternative,
    string JudgeRationale,
    int RetryIteration);

/// <summary>Stage 4 — solution draft.</summary>
[LoomContract("loom.autofix.solution")]
public sealed partial record SolutionDraft(
    string RunId,
    string? Repo,
    string? Diff,
    string? RegressionTest);

/// <summary>Stage 5 — confidence audit.</summary>
[LoomContract("loom.autofix.confidence")]
public sealed partial record ConfidenceAudit(
    string RunId,
    string Level,
    int ScoreSum,
    int EvidenceGate,
    int RegressionGate,
    int CompletenessGate,
    int SelfChallengeGate,
    bool RetryRequested);

/// <summary>Stage 6 — final assembled report (mirrors <see cref="AutofixReport" />).</summary>
[LoomContract("loom.autofix.report")]
public sealed partial record AutofixWorkflowResult(
    string RunId,
    AutofixReport Report);
