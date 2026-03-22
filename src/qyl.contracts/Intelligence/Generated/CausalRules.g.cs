// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/specs/intelligence/seed/rules.tsp
//     Spec:     specs/telemetry-intelligence.md §5.2
//     Rules:    6 v1 seed causal rules
// =============================================================================

namespace Qyl.Contracts.Intelligence;

/// <summary>
///     Static registry of all v1 seed causal rules.
///     Compile-time collections — no file I/O, no deserialization, no reflection.
/// </summary>
public static class CausalRules
{
    public static readonly IReadOnlyList<CausalRule> All =
    [
        new CausalRule
        {
            Id = "deploy_causes_regression",
            CausePattern = "deployment_regression",
            EffectPattern = "http_5xx_cluster",
            Strength = 0.85,
            TemporalWindow = "1h",
        },
        new CausalRule
        {
            Id = "rate_limit_causes_cascade",
            CausePattern = "genai_rate_limit",
            EffectPattern = "cascading_timeout",
            Strength = 0.70,
            TemporalWindow = "5m",
        },
        new CausalRule
        {
            Id = "db_timeout_causes_http_error",
            CausePattern = "db_timeout",
            EffectPattern = "http_5xx_cluster",
            Strength = 0.80,
            TemporalWindow = "1m",
        },
        new CausalRule
        {
            Id = "n_plus_one_causes_db_timeout",
            CausePattern = "db_n_plus_one",
            EffectPattern = "db_timeout",
            Strength = 0.75,
            TemporalWindow = "30s",
        },
        new CausalRule
        {
            Id = "memory_causes_timeout",
            CausePattern = "memory_pressure_latency",
            EffectPattern = "cascading_timeout",
            Strength = 0.65,
            TemporalWindow = "5m",
        },
        new CausalRule
        {
            Id = "token_exhaustion_causes_cost",
            CausePattern = "genai_token_exhaustion",
            EffectPattern = "cost_spike",
            Strength = 0.60,
            TemporalWindow = "1h",
        },
    ];
}
