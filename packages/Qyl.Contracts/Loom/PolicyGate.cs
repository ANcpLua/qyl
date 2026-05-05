namespace Qyl.Contracts.Loom;

public enum FixPolicy
{
    AutoApply,

    RequireReview,

    DryRun
}

public static class PolicyGate
{
    public const double DefaultAutoApplyThreshold = 0.85;

    public static string EvaluateNextStatus(FixPolicy policy, double confidenceScore,
        double threshold = DefaultAutoApplyThreshold) =>
        policy switch
        {
            FixPolicy.AutoApply when confidenceScore >= threshold => "applied",
            _ => "review"
        };
}
