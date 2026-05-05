
using Qyl.Hosting;
using Qyl.Instrumentation.Instrumentation.GenAi;
using Qyl.Instrumentation.Instrumentation.Inventory;
using Qyl.Loom.Patterns.Clients;

namespace Qyl.Loom.Patterns.Agents;

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

    private const string RcaDescription =
        "Synthesizes a 5-whys root-cause hypothesis from an IncidentSignal.";

    private const string SolutionDescription =
        "Turns a root-cause hypothesis into an ordered solution plan.";

    private const string ConfidenceDescription =
        "Approves or rejects a proposed solution plan.";

    public AIAgent BuildRcaAgent() =>
        clients.BuildChatClient("rca")
            .AsQylAgent("FakeRcaAgent", RcaDescription, RcaInstructions,
                b => b.UseQylAgentTelemetry(), services)
            .RecordInQylInventory(
                inventory,
                key: "FakeRcaAgent",
                instructions: RcaInstructions,
                description: RcaDescription);

    public AIAgent BuildSolutionAgent() =>
        clients.BuildChatClient("solution")
            .AsQylAgent("FakeSolutionAgent", SolutionDescription, SolutionInstructions,
                b => b.UseQylAgentTelemetry(), services)
            .RecordInQylInventory(
                inventory,
                key: "FakeSolutionAgent",
                instructions: SolutionInstructions,
                description: SolutionDescription);

    public AIAgent BuildConfidenceAgent() =>
        clients.BuildChatClient("verdict")
            .AsQylAgent("FakeConfidenceAgent", ConfidenceDescription, ConfidenceInstructions,
                b => b.UseQylAgentTelemetry(), services)
            .RecordInQylInventory(
                inventory,
                key: "FakeConfidenceAgent",
                instructions: ConfidenceInstructions,
                description: ConfidenceDescription);
}
