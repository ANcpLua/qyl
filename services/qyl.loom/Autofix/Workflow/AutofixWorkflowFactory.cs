
using Qyl.Loom.Agents;
using Qyl.Loom.Autofix.Workflow.Executors;

namespace Qyl.Loom.Autofix.Workflow;

internal sealed class AutofixWorkflowFactory(
    IQylLoomAgentsBuilder agents,
    AutofixContextLoader loader,
    AutofixReportAssemblyState state,
    IAutofixStepLedger ledger)
{
    private static readonly string[] s_perspectiveLenses =
    [
        "concurrency",
        "data-shape",
        "config-drift",
        "network-or-deploy",
        "code-recent-change"
    ];

    public Microsoft.Agents.AI.Workflows.Workflow Build(AutofixWorkflowConfig config)
    {
        var fixability = new FixabilityExecutor(
            "loom.autofix.fixability",
            agents.BuildFixabilityStageAgent(),
            state,
            ledger);

        var context = new ContextExecutor(
            "loom.autofix.context",
            agents.BuildContextStageAgent(config),
            loader,
            state,
            ledger);

        var fanOut = Math.Clamp(config.HypothesisFanOut, 1, s_perspectiveLenses.Length);
        var branches = new HypothesisExecutor[fanOut];
        for (var i = 0; i < fanOut; i++)
        {
            var perspective = s_perspectiveLenses[i];
            branches[i] = new HypothesisExecutor(
                $"loom.autofix.hypothesis.{perspective}",
                perspective,
                agents.BuildHypothesisBranchAgent(perspective),
                ledger);
        }

        var judge = new HypothesisJudgeExecutor(
            "loom.autofix.hypothesis.judge",
            agents.BuildHypothesisJudgeAgent(),
            state,
            ledger);

        var solution = new SolutionExecutor(
            "loom.autofix.solution",
            agents.BuildSolutionStageAgent(),
            state,
            ledger);

        var confidence = new ConfidenceExecutor(
            "loom.autofix.confidence",
            agents.BuildConfidenceStageAgent(),
            config,
            state,
            ledger);

        var report = new ReportExecutor(
            "loom.autofix.report",
            agents.BuildReportStageAgent(),
            state,
            ledger);

        var critique = new SelfCritiqueRouter("loom.autofix.self_critique", state);

        var workflowBuilder = new WorkflowBuilder(fixability)
            .AddEdge(fixability, context)
            .AddFanOutEdge<ContextSummary>(context, [.. branches], targetSelector: null)
            .AddFanInBarrierEdge([.. branches], judge);

        if (config.StoppingPointAfterHypothesis)
        {
            workflowBuilder = workflowBuilder
                .AddExternalCall<HypothesisVerdict, HypothesisVerdict>(judge, "loom.autofix.review.pre_solution")
                .ForwardMessage<HypothesisVerdict>("loom.autofix.review.pre_solution", [solution]);
        }
        else
        {
            workflowBuilder = workflowBuilder.AddEdge(judge, solution);
        }

        workflowBuilder = workflowBuilder.AddEdge(solution, confidence);

        ExecutorBinding commitTarget;
        if (config.StoppingPointBeforeCommit)
        {
            var bridge = new StoppingPointGateExecutor<ConfidenceAudit>("loom.autofix.gate.pre_commit");
            workflowBuilder = workflowBuilder
                .AddExternalCall<ConfidenceAudit, ConfidenceAudit>(bridge, "loom.autofix.review.pre_commit")
                .ForwardMessage<ConfidenceAudit>("loom.autofix.review.pre_commit", [report]);
            commitTarget = bridge;
        }
        else
        {
            commitTarget = report;
        }

        workflowBuilder = workflowBuilder
            .AddSwitch(confidence, sw => sw
                .AddCase<ConfidenceAudit>(audit => audit is { RetryRequested: true }, critique)
                .WithDefault(commitTarget))
            .AddFanOutEdge<ContextSummary>(critique, [.. branches], targetSelector: null);

        return workflowBuilder
            .WithOutputFrom(report)
            .WithName("Qyl.Loom.Autofix.Workflow")
            .Build();
    }
}
