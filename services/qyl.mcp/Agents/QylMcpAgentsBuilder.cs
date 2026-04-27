// Copyright (c) 2025-2026 ancplua

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Qyl.Instrumentation.Instrumentation.GenAi;
using qyl.mcp.Clients;

namespace qyl.mcp.Agents;

/// <summary>
///     Default <see cref="IQylMcpAgentsBuilder" />. Every returned agent is wrapped
///     with <c>.AsBuilder().UseQylAgentTelemetry().Build()</c> — the <c>QYL0135</c>
///     analyzer enforces this on every construction site. Meta-tool agents
///     (<see cref="BuildRcaAgent" />, <see cref="BuildUseQylAgent" />) source their
///     chat client from
///     <see cref="IQylMcpChatClientBuilder.BuildAgentChatClient" /> so qyl's
///     non-default <c>UseFunctionInvocation</c> tuning applies before the agent
///     wraps the client.
/// </summary>
internal sealed class QylMcpAgentsBuilder(IQylMcpChatClientBuilder clients) : IQylMcpAgentsBuilder
{
    private const string TestGenerationSystemPrompt = """
                                                      You are a test engineer. Generate a regression test that would catch the given error if it reoccurs.

                                                      Requirements:
                                                      - Write the test in the framework the user requests.
                                                      - Verify the specific behavior that caused the error.
                                                      - Include arrange/act/assert structure.
                                                      - Add comments explaining what the test validates.
                                                      - If the error is in a specific method, test that method's edge cases.
                                                      - Include setup code (mocks, fixtures) as needed.
                                                      - Output the complete test file in a single code block.
                                                      """;

    /// <inheritdoc />
    public bool IsConfigured => clients.IsConfigured;

    /// <inheritdoc />
    public AIAgent BuildSummarizeErrorAgent() =>
        Llm()
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name = "ErrorSummaryAgent",
                Description = "Produces a structured AI summary of a qyl error issue.",
                ChatOptions = new ChatOptions { Instructions = ErrorSummaryPrompt.Prompt }
            })
            .AsBuilder()
            .UseQylAgentTelemetry()
            .Build();

    /// <inheritdoc />
    public AIAgent BuildSummarizeTraceAgent() =>
        Llm()
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name = "TraceSummaryAgent",
                Description = "Produces a structured AI summary of a distributed trace.",
                ChatOptions = new ChatOptions { Instructions = TraceSummaryPrompt.Prompt }
            })
            .AsBuilder()
            .UseQylAgentTelemetry()
            .Build();

    /// <inheritdoc />
    public AIAgent BuildSummarizeSessionAgent() =>
        Llm()
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name = "SessionSummaryAgent",
                Description = "Produces a structured AI summary of a session's spans and lifecycle.",
                ChatOptions = new ChatOptions { Instructions = SessionSummaryPrompt.Prompt }
            })
            .AsBuilder()
            .UseQylAgentTelemetry()
            .Build();

    /// <inheritdoc />
    public AIAgent BuildTestGenerationAgent() =>
        Llm()
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name = "TestGenerationAgent",
                Description = "Generates regression tests that would catch a qyl error issue if it reoccurs.",
                ChatOptions = new ChatOptions { Instructions = TestGenerationSystemPrompt }
            })
            .AsBuilder()
            .UseQylAgentTelemetry()
            .Build();

    /// <inheritdoc />
    public AIAgent BuildAssistedQueryAgent(int rowLimit) =>
        Llm()
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name = "AssistedQueryAgent",
                Description = "Translates natural-language observability questions into DuckDB SELECT queries.",
                ChatOptions = new ChatOptions { Instructions = BuildSqlSystemPrompt(rowLimit) }
            })
            .AsBuilder()
            .UseQylAgentTelemetry()
            .Build();

    /// <inheritdoc />
    public AIAgent BuildRcaAgent(IReadOnlyList<AITool> tools) =>
        AgentLlm()
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name = "RcaAgent",
                Description =
                    "Multi-phase root-cause investigator with access to error, anomaly, span, and structured-log tools.",
                ChatOptions = new ChatOptions
                {
                    Instructions = RcaPrompt.Prompt,
                    Tools = [.. tools],
                    ToolMode = ChatToolMode.Auto
                }
            })
            .AsBuilder()
            .UseQylAgentTelemetry()
            .Build();

    /// <inheritdoc />
    public AIAgent BuildUseQylAgent(IReadOnlyList<AITool> tools) =>
        AgentLlm()
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name = "UseQylAgent",
                Description =
                    "Orchestrates qyl MCP tools under InvestigationLineage guard to answer observability questions.",
                ChatOptions = new ChatOptions
                {
                    Instructions = UseQylSystemPrompt.Prompt,
                    Tools = [.. tools],
                    ToolMode = ChatToolMode.Auto
                }
            })
            .AsBuilder()
            .UseQylAgentTelemetry()
            .Build();

    private IChatClient Llm() =>
        clients.BuildChatClient() ?? throw new InvalidOperationException(
            "qyl.mcp agent requested but no LLM provider configured. " +
            "Gate the call on IQylMcpAgentsBuilder.IsConfigured.");

    private IChatClient AgentLlm() =>
        clients.BuildAgentChatClient() ?? throw new InvalidOperationException(
            "qyl.mcp meta-tool agent requested but no LLM provider configured. " +
            "Gate the call on IQylMcpAgentsBuilder.IsConfigured.");

    private static string BuildSqlSystemPrompt(int limit) =>
        $$"""
          You are a DuckDB SQL expert for an observability platform. Generate a single SELECT query for the user's question.

          ## Available tables (key columns only)
          - spans: trace_id, span_id, service_name, span_name, duration_ms, status, start_time
          - errors: error_id, error_type, error_message, status, fingerprint, first_seen, last_seen, event_count, affected_services
          - logs: log_id, service_name, severity, body, timestamp
          - deployments: deployment_id, service_name, service_version, status, start_time
          - triage_results: triage_id, issue_id, fixability_score, automation_level, ai_summary
          - fix_runs: run_id, issue_id, status, confidence_score, changes_json, created_at
          - issue_events: event_id, issue_id, event_type, old_value, new_value, reason, created_at

          ## Rules
          - DuckDB syntax (supports LIMIT, window functions, array_agg, etc.)
          - SELECT only — no INSERT, UPDATE, DELETE, DDL
          - LIMIT {{limit}} maximum
          - Use now() for current time, interval for time ranges
          - Output ONLY the SQL in a ```sql code block, nothing else
          """;
}
