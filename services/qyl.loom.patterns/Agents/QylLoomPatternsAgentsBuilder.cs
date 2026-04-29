// Copyright (c) 2025-2026 ancplua

using Qyl.Instrumentation.Instrumentation.GenAi;
using Qyl.Instrumentation.Instrumentation.Inventory;
using Qyl.Loom.Patterns.Clients;

namespace Qyl.Loom.Patterns.Agents;

/// <summary>
///     Default <see cref="IQylLoomPatternsAgentsBuilder" />. Every returned agent is
///     wrapped with <c>.AsBuilder().UseQylAgentTelemetry().Build()</c> — the
///     <c>QYL0135</c> analyzer enforces this on every construction site.
/// </summary>
public sealed class QylLoomPatternsAgentsBuilder(
    IQylLoomPatternsChatClientBuilder clients,
    IServiceProvider services,
    IQylAgentInventory? inventory = null)
    : IQylLoomPatternsAgentsBuilder
{
    private const string RcaInstructions =
        "You are the RCA stage. Given a captured IncidentSignal, emit a " +
        "single-sentence root-cause hypothesis. No preamble.";

    private const string SolutionInstructions =
        "You are the Solution stage. Given an RCA hypothesis, emit a " +
        "terse step-by-step fix. No preamble.";

    private const string ConfidenceInstructions =
        "You are the Confidence stage. Given a plan, reply with 'approved' " +
        "or 'rejected' plus one reason. No preamble.";

    /// <inheritdoc />
    public AIAgent BuildRcaAgent() =>
        clients.BuildChatClient("rca")
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name = "FakeRcaAgent",
                Description = "Synthesizes a 5-whys root-cause hypothesis from an IncidentSignal.",
                ChatOptions = new ChatOptions { Instructions = RcaInstructions }
            })
            .AsBuilder()
            .UseQylAgentTelemetry()
            .Build(services)
            .RecordInQylInventory(
                inventory,
                key: "FakeRcaAgent",
                instructions: RcaInstructions,
                description: "Synthesizes a 5-whys root-cause hypothesis from an IncidentSignal.");

    /// <inheritdoc />
    public AIAgent BuildSolutionAgent() =>
        clients.BuildChatClient("solution")
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name = "FakeSolutionAgent",
                Description = "Turns a root-cause hypothesis into an ordered solution plan.",
                ChatOptions = new ChatOptions { Instructions = SolutionInstructions }
            })
            .AsBuilder()
            .UseQylAgentTelemetry()
            .Build(services)
            .RecordInQylInventory(
                inventory,
                key: "FakeSolutionAgent",
                instructions: SolutionInstructions,
                description: "Turns a root-cause hypothesis into an ordered solution plan.");

    /// <inheritdoc />
    public AIAgent BuildConfidenceAgent() =>
        clients.BuildChatClient("verdict")
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name = "FakeConfidenceAgent",
                Description = "Approves or rejects a proposed solution plan.",
                ChatOptions = new ChatOptions { Instructions = ConfidenceInstructions }
            })
            .AsBuilder()
            .UseQylAgentTelemetry()
            .Build(services)
            .RecordInQylInventory(
                inventory,
                key: "FakeConfidenceAgent",
                instructions: ConfidenceInstructions,
                description: "Approves or rejects a proposed solution plan.");
}
