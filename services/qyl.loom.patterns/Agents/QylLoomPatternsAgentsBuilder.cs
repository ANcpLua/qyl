// Copyright (c) 2025-2026 ancplua

using Qyl.Instrumentation.Instrumentation.GenAi;
using Qyl.Loom.Patterns.Clients;

namespace Qyl.Loom.Patterns.Agents;

/// <summary>
///     Default <see cref="IQylLoomPatternsAgentsBuilder"/>. Every returned agent is
///     wrapped with <c>.AsBuilder().UseQylAgentTelemetry().Build()</c> — the
///     <c>QYL0135</c> analyzer enforces this on every construction site.
/// </summary>
public sealed class QylLoomPatternsAgentsBuilder(
    IQylLoomPatternsChatClientBuilder clients,
    IServiceProvider services)
    : IQylLoomPatternsAgentsBuilder
{
    /// <inheritdoc/>
    public AIAgent BuildRcaAgent() =>
        clients.BuildChatClient("rca")
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name        = "FakeRcaAgent",
                Description = "Synthesizes a 5-whys root-cause hypothesis from an IncidentSignal.",
                ChatOptions = new ChatOptions
                {
                    Instructions =
                        "You are the RCA stage. Given a captured IncidentSignal, emit a " +
                        "single-sentence root-cause hypothesis. No preamble.",
                },
            })
            .AsBuilder()
            .UseQylAgentTelemetry()
            .Build(services);

    /// <inheritdoc/>
    public AIAgent BuildSolutionAgent() =>
        clients.BuildChatClient("solution")
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name        = "FakeSolutionAgent",
                Description = "Turns a root-cause hypothesis into an ordered solution plan.",
                ChatOptions = new ChatOptions
                {
                    Instructions =
                        "You are the Solution stage. Given an RCA hypothesis, emit a " +
                        "terse step-by-step fix. No preamble.",
                },
            })
            .AsBuilder()
            .UseQylAgentTelemetry()
            .Build(services);

    /// <inheritdoc/>
    public AIAgent BuildConfidenceAgent() =>
        clients.BuildChatClient("verdict")
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name        = "FakeConfidenceAgent",
                Description = "Approves or rejects a proposed solution plan.",
                ChatOptions = new ChatOptions
                {
                    Instructions =
                        "You are the Confidence stage. Given a plan, reply with 'approved' " +
                        "or 'rejected' plus one reason. No preamble.",
                },
            })
            .AsBuilder()
            .UseQylAgentTelemetry()
            .Build(services);
}
