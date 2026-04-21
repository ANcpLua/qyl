// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/specs/intelligence/seed/strategies.tsp
//     Spec:     specs/telemetry-intelligence.md §5.3
//     Strategies: 5 v1 seed investigation strategies (4 infra + 1 agent behavioral)
// =============================================================================

namespace Qyl.Contracts.Intelligence;

/// <summary>
///     Static registry of all v1 seed investigation strategies.
///     Compile-time collections — no file I/O, no deserialization, no reflection.
/// </summary>
public static class InvestigationStrategies
{
    public static readonly IReadOnlyList<InvestigationStrategy> All =
    [
        // -----------------------------------------------------------------
        // investigate_error_issue — 6 steps
        // Triggered by any error category pattern
        // -----------------------------------------------------------------
        new InvestigationStrategy
        {
            Id = "investigate_error_issue",
            TriggerPattern = "category:error",
            Steps =
            [
                new InvestigationStep
                {
                    Action = "get_issue",
                    Query = "SELECT * FROM error_issues WHERE id = ?",
                    Description = "Get issue summary, occurrence count, first/last seen",
                },
                new InvestigationStep
                {
                    Action = "get_events",
                    Query = "SELECT * FROM error_issue_events WHERE issue_id = ? ORDER BY timestamp DESC LIMIT 10",
                    Description = "Get recent error occurrences with trace IDs",
                },
                new InvestigationStep
                {
                    Action = "get_traces",
                    Query = "SELECT * FROM spans WHERE trace_id IN (?) ORDER BY start_time_unix_nano",
                    Description = "Reconstruct full trace graph for each occurrence",
                },
                new InvestigationStep
                {
                    Action = "get_code_location",
                    Query = "SELECT json_extract_string(attributes_json, '$.code.filepath') AS code_filepath, json_extract_string(attributes_json, '$.code.function') AS code_function, CAST(json_extract_string(attributes_json, '$.code.lineno') AS INTEGER) AS code_lineno FROM spans WHERE span_id = ?",
                    Description = "Map error to source file and function",
                },
                new InvestigationStep
                {
                    Action = "correlate_deployment",
                    Query = "SELECT * FROM deployments WHERE service_name = ? AND start_time <= ? ORDER BY start_time DESC LIMIT 1",
                    Description = "Find the deployment active when error occurred",
                },
                new InvestigationStep
                {
                    Action = "check_fix_history",
                    Query = "SELECT * FROM fix_runs WHERE issue_id = ?",
                    Description = "Check if this error class was fixed before",
                },
            ],
        },

        // -----------------------------------------------------------------
        // investigate_latency — 5 steps
        // Triggered by any latency category pattern
        // -----------------------------------------------------------------
        new InvestigationStrategy
        {
            Id = "investigate_latency",
            TriggerPattern = "category:latency",
            Steps =
            [
                new InvestigationStep
                {
                    Action = "identify_service",
                    Query = "SELECT service_name, AVG(duration_ns) AS avg_duration, PERCENTILE_CONT(0.99) WITHIN GROUP (ORDER BY duration_ns) AS p99 FROM spans GROUP BY service_name ORDER BY p99 DESC",
                    Description = "Find the slowest service",
                },
                new InvestigationStep
                {
                    Action = "compare_distributions",
                    Query = "SELECT duration_ns FROM spans WHERE service_name = ? AND start_time_unix_nano BETWEEN ? AND ?",
                    Description = "Compare current vs baseline latency distribution",
                },
                new InvestigationStep
                {
                    Action = "find_regression_window",
                    Query = "SELECT time_bucket('5 minute', to_timestamp(start_time_unix_nano / 1000000000)) AS bucket, PERCENTILE_CONT(0.99) WITHIN GROUP (ORDER BY duration_ns) AS p99 FROM spans WHERE service_name = ? GROUP BY bucket ORDER BY bucket",
                    Description = "Identify when latency degraded via time-series p99 analysis",
                },
                new InvestigationStep
                {
                    Action = "correlate_deployment",
                    Query = "SELECT * FROM deployments WHERE service_name = ? AND start_time <= ? ORDER BY start_time DESC LIMIT 1",
                    Description = "Find deployment in the regression window",
                },
                new InvestigationStep
                {
                    Action = "inspect_slow_spans",
                    Query = "SELECT * FROM spans WHERE service_name = ? AND duration_ns > ? ORDER BY duration_ns DESC LIMIT 20",
                    Description = "Examine the slowest individual spans",
                },
            ],
        },

        // -----------------------------------------------------------------
        // investigate_cost — 5 steps
        // Triggered by any cost category pattern
        // -----------------------------------------------------------------
        new InvestigationStrategy
        {
            Id = "investigate_cost",
            TriggerPattern = "category:cost",
            Steps =
            [
                new InvestigationStep
                {
                    Action = "identify_model",
                    Query = "SELECT gen_ai_request_model, SUM(gen_ai_cost_usd) AS total_cost, COUNT(*) AS call_count FROM spans WHERE gen_ai_cost_usd IS NOT NULL GROUP BY gen_ai_request_model ORDER BY total_cost DESC",
                    Description = "Find the most expensive model",
                },
                new InvestigationStep
                {
                    Action = "identify_service",
                    Query = "SELECT service_name, SUM(gen_ai_cost_usd) AS total_cost, COUNT(*) AS call_count FROM spans WHERE gen_ai_cost_usd IS NOT NULL GROUP BY service_name ORDER BY total_cost DESC",
                    Description = "Find the most expensive service",
                },
                new InvestigationStep
                {
                    Action = "identify_session",
                    Query = "SELECT session_id, SUM(gen_ai_cost_usd) AS total_cost FROM spans WHERE gen_ai_cost_usd IS NOT NULL AND session_id IS NOT NULL GROUP BY session_id ORDER BY total_cost DESC LIMIT 10",
                    Description = "Find the most expensive sessions",
                },
                new InvestigationStep
                {
                    Action = "trace_to_root",
                    Query = "SELECT * FROM spans WHERE session_id = ? ORDER BY start_time_unix_nano",
                    Description = "Understand what operations drove the cost by tracing session spans",
                },
                new InvestigationStep
                {
                    Action = "compare_to_baseline",
                    Query = "SELECT DATE_TRUNC('day', to_timestamp(start_time_unix_nano / 1000000000)) AS day, SUM(gen_ai_cost_usd) AS daily_cost FROM spans WHERE gen_ai_cost_usd IS NOT NULL GROUP BY day ORDER BY day DESC LIMIT 14",
                    Description = "Quantify the cost increase by comparing current vs previous period",
                },
            ],
        },

        // -----------------------------------------------------------------
        // investigate_genai — 5 steps
        // Triggered by any genai category pattern
        // -----------------------------------------------------------------
        new InvestigationStrategy
        {
            Id = "investigate_genai",
            TriggerPattern = "category:genai",
            Steps =
            [
                new InvestigationStep
                {
                    Action = "get_error_details",
                    Query = "SELECT * FROM spans WHERE status_code = 2 AND gen_ai_provider_name IS NOT NULL ORDER BY start_time_unix_nano DESC LIMIT 20",
                    Description = "Get GenAI error spans with full context",
                },
                new InvestigationStep
                {
                    Action = "check_provider_status",
                    Query = "SELECT gen_ai_provider_name, COUNT(*) AS error_count, MIN(start_time_unix_nano) AS first_error, MAX(start_time_unix_nano) AS last_error FROM spans WHERE status_code = 2 AND gen_ai_provider_name IS NOT NULL GROUP BY gen_ai_provider_name",
                    Description = "Determine if this is a provider-wide issue by checking error frequency",
                },
                new InvestigationStep
                {
                    Action = "analyze_token_usage",
                    Query = "SELECT gen_ai_input_tokens, gen_ai_output_tokens, gen_ai_request_model FROM spans WHERE gen_ai_request_model = ? AND gen_ai_input_tokens IS NOT NULL ORDER BY start_time_unix_nano DESC LIMIT 50",
                    Description = "Check if approaching model token limits",
                },
                new InvestigationStep
                {
                    Action = "check_prompt_patterns",
                    Query = "SELECT gen_ai_input_tokens, start_time_unix_nano FROM spans WHERE gen_ai_request_model = ? AND gen_ai_input_tokens IS NOT NULL ORDER BY start_time_unix_nano DESC LIMIT 100",
                    Description = "Identify if prompts are growing unbounded by inspecting token trends",
                },
                new InvestigationStep
                {
                    Action = "suggest_mitigation",
                    Query = "pattern_specific_recommendation",
                    Description = "Rate limit: add backoff. Token limit: truncation. Content filter: prompt review.",
                },
            ],
        },
        // -----------------------------------------------------------------
        // investigate_agent_failure — 6 steps
        // Triggered by any agent category pattern (AgentRx taxonomy)
        // -----------------------------------------------------------------
        new InvestigationStrategy
        {
            Id = "investigate_agent_failure",
            TriggerPattern = "category:agent",
            Steps =
            [
                new InvestigationStep
                {
                    Action = "get_agent_trace",
                    Query = "SELECT * FROM spans WHERE trace_id = ? AND gen_ai_agent_name IS NOT NULL ORDER BY start_time_unix_nano",
                    Description = "Reconstruct the full agent execution trajectory",
                },
                new InvestigationStep
                {
                    Action = "get_tool_calls",
                    Query = "SELECT * FROM spans WHERE trace_id = ? AND gen_ai_tool_name IS NOT NULL ORDER BY start_time_unix_nano",
                    Description = "Extract all tool invocations — inputs, outputs, errors",
                },
                new InvestigationStep
                {
                    Action = "classify_failure_mode",
                    Query = "pattern_specific_classification",
                    Description = "Classify into AgentRx taxonomy: intent misalignment, tool misinterpretation, hallucination, policy violation, plan adherence, invalid invocation, instruction failure, underspecified intent",
                },
                new InvestigationStep
                {
                    Action = "trace_decision_points",
                    Query = "SELECT * FROM spans WHERE trace_id = ? AND (gen_ai_operation_name = 'invoke_agent' OR gen_ai_tool_name IS NOT NULL) ORDER BY start_time_unix_nano",
                    Description = "Identify where the agent made key decisions and which ones diverged from expected behavior",
                },
                new InvestigationStep
                {
                    Action = "check_invariants",
                    Query = "pattern_specific_invariant_check",
                    Description = "Verify correctness invariants: did tool outputs get used? Did the agent stay within scope? Did claims match evidence?",
                },
                new InvestigationStep
                {
                    Action = "suggest_remediation",
                    Query = "pattern_specific_recommendation",
                    Description = "Intent misalignment: refine system prompt. Tool misuse: improve schema/examples. Hallucination: add grounding step. Policy: content filter. Plan drift: add checkpoints.",
                },
            ],
        },
    ];
}
