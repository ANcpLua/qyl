// Copyright (c) 2025-2026 ancplua

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Qyl.Instrumentation.Instrumentation.GenAi;
using Qyl.Loom.Autofix;
using Qyl.Loom.Clients;
using Qyl.Loom.Exploration;

namespace Qyl.Loom.Agents;

/// <summary>
///     Default <see cref="IQylLoomAgentsBuilder" />. Every returned agent is wrapped
///     with <c>.AsBuilder().UseQylAgentTelemetry().Build()</c> — the
///     <c>QYL0135</c> analyzer enforces this on every construction site.
/// </summary>
internal sealed class QylLoomAgentsBuilder(IQylLoomChatClientBuilder clients) : IQylLoomAgentsBuilder
{
    private readonly IChatClient? _llm = clients.BuildChatClient();

    /// <inheritdoc />
    public bool IsConfigured => _llm is not null;

    /// <inheritdoc />
    public AIAgent BuildTriageScoringAgent() =>
        Llm()
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name = "TriageScoringAgent",
                Description = "Scores the fixability of a qyl error issue and proposes an automation level.",
                ChatOptions = new ChatOptions { Instructions = TriagePrompts.FixabilityScoring }
            })
            .AsBuilder()
            .UseQylAgentTelemetry()
            .Build();

    /// <inheritdoc />
    public AIAgent BuildCodeReviewAgent() =>
        Llm()
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name = "CodeReviewAgent",
                Description = "Reviews a pull request diff and emits structured JSON comments."
                // No Instructions: callers deliver the system-role preamble + output contract
                // as a single user-turn prompt (CodeReviewPrompt.Build(...) MCP resource).
            })
            .AsBuilder()
            .UseQylAgentTelemetry()
            .Build();

    /// <inheritdoc />
    public AIAgent BuildAutofixAgent() =>
        Llm()
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name = "LoomAutofix",
                Description = "Loom headless autofix runner — five-stage contract, schema-enforced output.",
                ChatOptions = new ChatOptions
                {
                    Instructions = LoomAutofixPrompts.SystemPrompt,
                    ResponseFormat = ChatResponseFormat.ForJsonSchema<AutofixReport>()
                }
            })
            .AsBuilder()
            .UseQylAgentTelemetry()
            .Build();

    /// <inheritdoc />
    public AIAgent BuildExplorationInsightAgent() =>
        Llm()
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name = "ExplorationInsightAgent",
                Description =
                    "Produces a pre-investigation insight summary (what happened / initial guess / in the trace).",
                ChatOptions = new ChatOptions { Instructions = ExplorationPrompts.InsightGeneration }
            })
            .AsBuilder()
            .UseQylAgentTelemetry()
            .Build();

    /// <inheritdoc />
    public AIAgent BuildExplorationStrategistAgent() =>
        Llm()
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name = "ExplorationStrategistAgent",
                Description = "Converts an exploration root-cause analysis into a minimal implementation plan.",
                ChatOptions = new ChatOptions { Instructions = ExplorationPrompts.SolutionPlanning }
            })
            .AsBuilder()
            .UseQylAgentTelemetry()
            .Build();

    private IChatClient Llm() =>
        _llm ?? throw new InvalidOperationException(
            "qyl.loom agent requested but no LLM provider configured. " +
            "Gate the call on IQylLoomAgentsBuilder.IsConfigured.");
}
