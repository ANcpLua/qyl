
using Qyl.Hosting;
using Qyl.Instrumentation.Instrumentation.GenAi;
using Qyl.Instrumentation.Instrumentation.Inventory;
using Qyl.Loom.Autofix.Workflow;
using Qyl.Loom.Clients;
using Qyl.Loom.Exploration;

namespace Qyl.Loom.Agents;

internal sealed class QylLoomAgentsBuilder(
    IQylLoomChatClientBuilder clients,
    AutofixContextTools contextTools,
    IQylAgentInventory? inventory = null)
    : IQylLoomAgentsBuilder
{
    private readonly IChatClient? _llm = clients.BuildChatClient();

    public bool IsConfigured => _llm is not null;

    public AIAgent BuildTriageScoringAgent() =>
        Compose("TriageScoringAgent",
            "Scores the fixability of a qyl error issue and proposes an automation level.",
            TriagePrompts.FixabilityScoring);

    public AIAgent BuildCodeReviewAgent() =>
        Compose("CodeReviewAgent",
            "Reviews a pull request diff and emits structured JSON comments.",
            instructions: null);

    public AIAgent BuildExplorationInsightAgent() =>
        Compose("ExplorationInsightAgent",
            "Produces a pre-investigation insight summary (what happened / initial guess / in the trace).",
            ExplorationPrompts.InsightGeneration);

    public AIAgent BuildExplorationStrategistAgent() =>
        Compose("ExplorationStrategistAgent",
            "Converts an exploration root-cause analysis into a minimal implementation plan.",
            ExplorationPrompts.SolutionPlanning);

    public AIAgent BuildFixabilityStageAgent() =>
        Compose("LoomAutofix.Fixability",
            "Autofix Stage 1 — fixability gate.",
            AutofixStagePrompts.Fixability);

    public AIAgent BuildContextStageAgent(AutofixWorkflowConfig config)
    {
        if (!config.ToolUsingContext)
        {
            return Compose("LoomAutofix.Context",
                "Autofix Stage 2 — context gathering (static).",
                AutofixStagePrompts.Context);
        }

        var tools = AutofixContextToolFactories
            .Create(contextTools)
            .Cast<AITool>()
            .ToArray();

        var options = new ChatClientAgentOptions
        {
            Name = "LoomAutofix.Context",
            Description = "Autofix Stage 2 — tool-using context gathering.",
            ChatOptions = new ChatOptions
            {
                Instructions = AutofixStagePrompts.Context,
                Tools = tools,
                ToolMode = ChatToolMode.Auto
            }
        };

        var pipeline = new ChatClientBuilder(Llm())
            .UseFunctionInvocation(configure: invoker =>
            {
                invoker.MaximumIterationsPerRequest = config.ContextToolBudget;
                invoker.AllowConcurrentInvocation = false;
            })
            .Build();

        return pipeline
            .AsAIAgent(options)
            .AsBuilder()
            .UseQylAgentTelemetry()
            .Build()
            .RecordInQylInventory(
                inventory,
                key: "LoomAutofix.Context",
                instructions: AutofixStagePrompts.Context,
                description: "Autofix Stage 2 — tool-using context gathering.");
    }

    public AIAgent BuildHypothesisBranchAgent(string perspective) =>
        Compose($"LoomAutofix.Hypothesis.{perspective}",
            $"Autofix Stage 3 — single-perspective hypothesis branch ({perspective}).",
            AutofixStagePrompts.Hypothesis);

    public AIAgent BuildHypothesisJudgeAgent() =>
        Compose("LoomAutofix.HypothesisJudge",
            "Autofix Stage 3 — fan-in judge: picks the winning hypothesis candidate.",
            AutofixStagePrompts.HypothesisJudge);

    public AIAgent BuildSolutionStageAgent() =>
        Compose("LoomAutofix.Solution",
            "Autofix Stage 4 — minimal patch + regression test.",
            AutofixStagePrompts.Solution);

    public AIAgent BuildConfidenceStageAgent() =>
        Compose("LoomAutofix.Confidence",
            "Autofix Stage 5 — four-gate confidence audit.",
            AutofixStagePrompts.Confidence);

    public AIAgent BuildReportStageAgent() =>
        Compose("LoomAutofix.Report",
            "Autofix Stage 6 — final 200-word handoff.",
            instructions: null);

    private AIAgent Compose(string name, string description, string? instructions)
    {
        AIAgent agent = instructions is null
            ? Llm()
                .AsAIAgent(new ChatClientAgentOptions { Name = name, Description = description })
                .AsBuilder()
                .UseQylAgentTelemetry()
                .Build()
            : Llm().AsQylAgent(name, description, instructions, b => b.UseQylAgentTelemetry());

        return agent.RecordInQylInventory(
            inventory,
            key: name,
            instructions: instructions,
            description: description);
    }

    private IChatClient Llm() =>
        _llm ?? throw new InvalidOperationException(
            "qyl.loom agent requested but no LLM provider configured. " +
            "Gate the call on IQylLoomAgentsBuilder.IsConfigured.");
}
