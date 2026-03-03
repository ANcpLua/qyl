namespace qyl.collector.Autofix;

/// <summary>
///     LLM prompt templates for the Seer triage pipeline.
/// </summary>
internal static class TriagePrompts
{
    public const string FixabilityScoring = """
                                            You are an AI debugging assistant triaging software errors.
                                            Analyze the error below and return a JSON object with exactly these fields:

                                            {
                                              "fixability_score": <float 0.0 to 1.0>,
                                              "automation_level": "<auto|assisted|manual|skip>",
                                              "root_cause_hypothesis": "<one sentence>",
                                              "summary": "<2-3 sentence summary of the error and likely fix>"
                                            }

                                            Scoring guidance:
                                            - 0.8-1.0 (auto): Clear root cause, well-known fix pattern, single file change
                                            - 0.5-0.79 (assisted): Likely fixable but needs human review, multi-file change
                                            - 0.2-0.49 (manual): Complex issue, unclear root cause, architectural change needed
                                            - 0.0-0.19 (skip): Not actionable, environmental issue, or insufficient context

                                            Error details:
                                            """;
}

/// <summary>
///     Storage record for a triage result. Maps to the triage_results DuckDB table.
/// </summary>
public sealed record TriageResult
{
    public required string TriageId { get; init; }
    public required string IssueId { get; init; }
    public required double FixabilityScore { get; init; }
    public required string AutomationLevel { get; init; }
    public string? AiSummary { get; init; }
    public string? RootCauseHypothesis { get; init; }
    public required string TriggeredBy { get; init; }
    public string? FixRunId { get; init; }
    public required string ScoringMethod { get; init; }
    public DateTime? CreatedAt { get; init; }
}
