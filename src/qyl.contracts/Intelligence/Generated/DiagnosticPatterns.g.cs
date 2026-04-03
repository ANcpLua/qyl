// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/specs/intelligence/seed/patterns.tsp
//     Spec:     specs/telemetry-intelligence.md §5.1
//     Patterns: 19 v1 seed diagnostic patterns (10 infra + 9 agent behavioral)
// =============================================================================

namespace Qyl.Contracts.Intelligence;

/// <summary>
///     Static registry of all v1 seed diagnostic patterns.
///     Compile-time collections — no file I/O, no deserialization, no reflection.
/// </summary>
public static class DiagnosticPatterns
{
    public static readonly IReadOnlyList<DiagnosticPattern> All =
    [
        // -----------------------------------------------------------------
        // GenAI patterns
        // -----------------------------------------------------------------
        new DiagnosticPattern
        {
            Id = "genai_rate_limit",
            Category = PatternCategory.GenAi,
            Signals =
            [
                new Signal { Attribute = "status_code", Operator = SignalOperator.Eq, Value = "2" },
                new Signal { Attribute = "gen_ai_provider_name", Operator = SignalOperator.Exists },
                new Signal { Attribute = "error_type", Operator = SignalOperator.Contains, Value = "rate_limit" },
            ],
            Hypothesis = "LLM provider is throttling requests. Check quota, reduce concurrency, or add backoff.",
            Confidence = 0.9,
        },
        new DiagnosticPattern
        {
            Id = "genai_token_exhaustion",
            Category = PatternCategory.GenAi,
            Signals =
            [
                new Signal { Attribute = "gen_ai_stop_reason", Operator = SignalOperator.Eq, Value = "length" },
            ],
            Hypothesis = "Context window exceeded. Reduce prompt size or switch to larger model.",
            Confidence = 0.85,
        },
        new DiagnosticPattern
        {
            Id = "genai_content_filter",
            Category = PatternCategory.GenAi,
            Signals =
            [
                new Signal { Attribute = "gen_ai_stop_reason", Operator = SignalOperator.Contains, Value = "content_filter" },
            ],
            Hypothesis = "Content policy violation. Review prompt content.",
            Confidence = 0.95,
        },

        // -----------------------------------------------------------------
        // Data patterns
        // -----------------------------------------------------------------
        new DiagnosticPattern
        {
            Id = "db_timeout",
            Category = PatternCategory.Data,
            Signals =
            [
                new Signal { Attribute = "exception_type", Operator = SignalOperator.Eq, Value = "TimeoutException" },
                new Signal { Attribute = "db.system.name", Operator = SignalOperator.Exists },
                new Signal { Attribute = "duration_ns", Operator = SignalOperator.Gt, Value = "2000000000" },
            ],
            Hypothesis = "Database query timeout. Check query plan, connection pool, lock contention.",
            Confidence = 0.85,
        },
        new DiagnosticPattern
        {
            Id = "db_n_plus_one",
            Category = PatternCategory.Data,
            Signals =
            [
                new Signal { Attribute = "db.system.name", Operator = SignalOperator.Exists },
                new Signal { Attribute = "parent_span_id", Operator = SignalOperator.Exists },
                new Signal { Attribute = "span_count_under_parent", Operator = SignalOperator.Gt, Value = "10" },
            ],
            Hypothesis = "N+1 query pattern. Batch or prefetch related data.",
            Confidence = 0.80,
        },

        // -----------------------------------------------------------------
        // Error patterns
        // -----------------------------------------------------------------
        new DiagnosticPattern
        {
            Id = "http_5xx_cluster",
            Category = PatternCategory.Error,
            Signals =
            [
                new Signal { Attribute = "http.response.status_code", Operator = SignalOperator.Gte, Value = "500" },
                new Signal { Attribute = "occurrence_rate", Operator = SignalOperator.Gt, Value = "baseline*3" },
            ],
            Hypothesis = "Server error spike. Check recent deployments and upstream dependencies.",
            Confidence = 0.75,
        },
        new DiagnosticPattern
        {
            Id = "deployment_regression",
            Category = PatternCategory.Error,
            Signals =
            [
                new Signal { Attribute = "error_type", Operator = SignalOperator.Exists },
                new Signal { Attribute = "first_seen_at", Operator = SignalOperator.Gt, Value = "last_deployment_time" },
            ],
            Hypothesis = "New error class after deployment. Compare with previous version.",
            Confidence = 0.80,
        },

        // -----------------------------------------------------------------
        // Latency patterns
        // -----------------------------------------------------------------
        new DiagnosticPattern
        {
            Id = "cascading_timeout",
            Category = PatternCategory.Latency,
            Signals =
            [
                new Signal { Attribute = "exception_type", Operator = SignalOperator.Contains, Value = "Timeout" },
                new Signal { Attribute = "downstream_service_error", Operator = SignalOperator.Eq, Value = "true" },
            ],
            Hypothesis = "Upstream failure causing downstream timeouts. Investigate root service first.",
            Confidence = 0.70,
        },
        new DiagnosticPattern
        {
            Id = "memory_pressure_latency",
            Category = PatternCategory.Latency,
            Signals =
            [
                new Signal { Attribute = "process.runtime.dotnet.gc.duration", Operator = SignalOperator.Gt, Value = "100" },
                new Signal { Attribute = "avg_latency", Operator = SignalOperator.Gt, Value = "p99_baseline" },
            ],
            Hypothesis = "GC pressure causing latency. Check memory allocation patterns.",
            Confidence = 0.65,
        },

        // -----------------------------------------------------------------
        // Cost patterns
        // -----------------------------------------------------------------
        new DiagnosticPattern
        {
            Id = "cost_spike",
            Category = PatternCategory.Cost,
            Signals =
            [
                new Signal { Attribute = "gen_ai_cost_usd", Operator = SignalOperator.Gt, Value = "daily_average*3" },
            ],
            Hypothesis = "Abnormal cost increase. Identify the model, service, and session responsible.",
            Confidence = 0.75,
        },

        // -----------------------------------------------------------------
        // Agent behavioral patterns (AgentRx taxonomy)
        // Source: Microsoft Research AgentRx, March 2026
        // -----------------------------------------------------------------
        new DiagnosticPattern
        {
            Id = "agent_intent_plan_misalignment",
            Category = PatternCategory.Agent,
            Signals =
            [
                new Signal { Attribute = "gen_ai.agent.name", Operator = SignalOperator.Exists },
                new Signal { Attribute = "gen_ai.operation.name", Operator = SignalOperator.Eq, Value = "invoke_agent" },
                new Signal { Attribute = "status_code", Operator = SignalOperator.Eq, Value = "2" },
            ],
            Hypothesis = "Agent produced a plan that diverges from the user's actual intent. Review the task decomposition against the original request.",
            Confidence = 0.70,
        },
        new DiagnosticPattern
        {
            Id = "agent_misinterpret_tool_info",
            Category = PatternCategory.Agent,
            Signals =
            [
                new Signal { Attribute = "gen_ai.tool.name", Operator = SignalOperator.Exists },
                new Signal { Attribute = "gen_ai.tool.call.id", Operator = SignalOperator.Exists },
                new Signal { Attribute = "error_type", Operator = SignalOperator.Contains, Value = "tool" },
            ],
            Hypothesis = "Agent misinterpreted tool output or schema. Check tool response parsing and whether the agent used the result correctly.",
            Confidence = 0.75,
        },
        new DiagnosticPattern
        {
            Id = "agent_rai_policy_violation",
            Category = PatternCategory.Agent,
            Signals =
            [
                new Signal { Attribute = "gen_ai.agent.name", Operator = SignalOperator.Exists },
                new Signal { Attribute = "gen_ai.stop_reason", Operator = SignalOperator.Contains, Value = "content_filter" },
            ],
            Hypothesis = "Agent triggered a responsible AI policy violation. Review the generated content and prompt chain for policy-prohibited material.",
            Confidence = 0.95,
        },
        new DiagnosticPattern
        {
            Id = "agent_plan_adherence_failure",
            Category = PatternCategory.Agent,
            Signals =
            [
                new Signal { Attribute = "gen_ai.agent.name", Operator = SignalOperator.Exists },
                new Signal { Attribute = "gen_ai.operation.name", Operator = SignalOperator.Eq, Value = "invoke_agent" },
                new Signal { Attribute = "gen_ai.usage.output_tokens", Operator = SignalOperator.Gt, Value = "0" },
                new Signal { Attribute = "child_span_count", Operator = SignalOperator.Gt, Value = "20" },
            ],
            Hypothesis = "Agent deviated from its own plan mid-execution. Excessive tool calls suggest the agent lost track of its strategy and started improvising.",
            Confidence = 0.65,
        },
        new DiagnosticPattern
        {
            Id = "agent_invent_new_info",
            Category = PatternCategory.Agent,
            Signals =
            [
                new Signal { Attribute = "gen_ai.agent.name", Operator = SignalOperator.Exists },
                new Signal { Attribute = "gen_ai.operation.name", Operator = SignalOperator.Eq, Value = "invoke_agent" },
            ],
            Hypothesis = "Agent fabricated information not present in tool outputs or context. Cross-reference agent claims against actual tool call results in the trace.",
            Confidence = 0.60,
        },
        new DiagnosticPattern
        {
            Id = "agent_invalid_invocation",
            Category = PatternCategory.Agent,
            Signals =
            [
                new Signal { Attribute = "gen_ai.tool.name", Operator = SignalOperator.Exists },
                new Signal { Attribute = "error_type", Operator = SignalOperator.Contains, Value = "invalid" },
                new Signal { Attribute = "gen_ai.agent.name", Operator = SignalOperator.Exists },
            ],
            Hypothesis = "Agent called a tool with invalid arguments or nonexistent tool name. Check tool schema alignment and parameter validation.",
            Confidence = 0.85,
        },
        new DiagnosticPattern
        {
            Id = "agent_hallucination_doubt",
            Category = PatternCategory.Agent,
            Signals =
            [
                new Signal { Attribute = "gen_ai.agent.name", Operator = SignalOperator.Exists },
                new Signal { Attribute = "gen_ai.usage.output_tokens", Operator = SignalOperator.Gt, Value = "500" },
                new Signal { Attribute = "gen_ai.tool.call.id", Operator = SignalOperator.NotExists },
            ],
            Hypothesis = "Agent produced a long response without grounding in tool calls. High risk of hallucinated content. Verify claims against available data sources.",
            Confidence = 0.55,
        },
        new DiagnosticPattern
        {
            Id = "agent_instruction_adherence_failure",
            Category = PatternCategory.Agent,
            Signals =
            [
                new Signal { Attribute = "gen_ai.agent.name", Operator = SignalOperator.Exists },
                new Signal { Attribute = "gen_ai.operation.name", Operator = SignalOperator.Eq, Value = "invoke_agent" },
            ],
            Hypothesis = "Agent ignored or contradicted explicit instructions in its system prompt. Compare agent behavior against the system prompt constraints.",
            Confidence = 0.60,
        },
        new DiagnosticPattern
        {
            Id = "agent_underspecified_intent",
            Category = PatternCategory.Agent,
            Signals =
            [
                new Signal { Attribute = "gen_ai.agent.name", Operator = SignalOperator.Exists },
                new Signal { Attribute = "gen_ai.operation.name", Operator = SignalOperator.Eq, Value = "invoke_agent" },
                new Signal { Attribute = "gen_ai.usage.input_tokens", Operator = SignalOperator.Lt, Value = "50" },
            ],
            Hypothesis = "Agent received an ambiguous or underspecified request. The short input suggests insufficient context for reliable execution. Consider adding clarification step.",
            Confidence = 0.65,
        },
    ];
}
