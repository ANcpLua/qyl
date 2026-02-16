namespace qyl.collector.Autofix;

/// <summary>
///     Approval policy for autofix runs. Controls whether fixes are
///     automatically applied, require human review, or are dry-run only.
/// </summary>
public enum FixPolicy
{
    /// <summary>Apply fixes automatically if confidence exceeds threshold.</summary>
    AutoApply,

    /// <summary>Always require human review before applying.</summary>
    RequireReview,

    /// <summary>Generate fix but do not apply — preview only.</summary>
    DryRun
}

/// <summary>
///     Evaluates whether a fix run result should be auto-applied based on policy and confidence.
/// </summary>
public static class PolicyGate
{
    /// <summary>Default confidence threshold for auto-apply policy.</summary>
    public const double DefaultAutoApplyThreshold = 0.85;

    /// <summary>
    ///     Determines whether a fix should be applied given the policy and confidence score.
    /// </summary>
    /// <returns><c>true</c> if the fix should be applied; <c>false</c> if review is needed.</returns>
    public static bool ShouldApply(FixPolicy policy, double confidenceScore,
        double threshold = DefaultAutoApplyThreshold) =>
        policy switch
        {
            FixPolicy.AutoApply => confidenceScore >= threshold,
            FixPolicy.RequireReview => false,
            FixPolicy.DryRun => false,
            _ => false
        };

    /// <summary>
    ///     Returns the next status based on policy evaluation.
    /// </summary>
    public static string EvaluateNextStatus(FixPolicy policy, double confidenceScore,
        double threshold = DefaultAutoApplyThreshold) =>
        policy switch
        {
            FixPolicy.AutoApply when confidenceScore >= threshold => "applied",
            FixPolicy.AutoApply => "review",
            FixPolicy.RequireReview => "review",
            FixPolicy.DryRun => "review",
            _ => "review"
        };
}

/// <summary>
///     Storage record for a fix run. Maps to the fix_runs DuckDB table.
/// </summary>
public sealed record FixRunRecord
{
    public required string RunId { get; init; }
    public required string IssueId { get; init; }
    public string? ExecutionId { get; init; }
    public required string Status { get; init; }
    public required string Policy { get; init; }
    public string? FixDescription { get; init; }
    public double? ConfidenceScore { get; init; }
    public string? ChangesJson { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}
