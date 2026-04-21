using Qyl.Instrumentation.Instrumentation.Loom;

namespace Qyl.Loom.Tools;

/// <summary>
///     Detect-phase tools. Pure functions — no LLM, no side effects.
///     Generator emits descriptors, JSON schemas, and registry entries.
/// </summary>
public static partial class LoomDetectors
{
    [LoomTool("score_issue_fixability",
        Description = "Score an issue's fixability using heuristic analysis of error type, frequency, and recency.",
        Phase = LoomPhase.Detect,
        UseOnlyWhen = "Evaluating whether an error issue is worth auto-fixing",
        DoNotUseWhen = "Issue already has a triage result or LLM scoring is available")]
    [RequiresCapability("qyl.triage.score")]
    [ToolSideEffect(ToolSideEffect.None)]
    public static TriageResult ScoreIssueFixability(IssueSummary issue) =>
        TriagePipelineService.ScoreWithHeuristic(issue);

    [LoomTool("derive_automation_level",
        Description = "Map a fixability score to an automation level (auto/assisted/manual/skip).",
        Phase = LoomPhase.Detect,
        UseOnlyWhen = "Converting a numeric score to an actionable routing decision",
        DoNotUseWhen = "Score not yet computed")]
    [ToolSideEffect(ToolSideEffect.None)]
    public static string DeriveAutomationLevel(double fixabilityScore) =>
        TriagePipelineService.DeriveAutomationLevel(fixabilityScore);
}
