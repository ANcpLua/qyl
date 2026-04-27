namespace Qyl.Contracts.Loom;

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
    ///     Returns the next status based on policy evaluation.
    /// </summary>
    public static string EvaluateNextStatus(FixPolicy policy, double confidenceScore,
        double threshold = DefaultAutoApplyThreshold) =>
        policy switch
        {
            FixPolicy.AutoApply when confidenceScore >= threshold => "applied",
            _ => "review"
        };
}
