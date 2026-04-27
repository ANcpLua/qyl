// Copyright (c) 2025-2026 ancplua

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

        // Initial linear path:
        //   fixability → context → fan-out → branches → fan-in barrier → judge
        var workflowBuilder = new WorkflowBuilder(fixability)
            .AddEdge(fixability, context)
            .AddFanOutEdge<ContextSummary>(context, [.. branches], targetSelector: null)
            .AddFanInBarrierEdge([.. branches], judge);

        // Stopping point #1 — pre-solution review (HITL).
        if (config.StoppingPointAfterHypothesis)
        {
            var gate = new StoppingPointGateExecutor<HypothesisVerdict>(
                "loom.autofix.gate.pre_solution", "pre_solution");
            workflowBuilder = workflowBuilder
                .AddEdge(judge, gate)
                .AddExternalCall<HypothesisVerdict, HypothesisVerdict>(gate, "loom.autofix.review.pre_solution")
                .ForwardMessage<HypothesisVerdict>("loom.autofix.review.pre_solution", [solution]);
        }
        else
        {
            workflowBuilder = workflowBuilder.AddEdge(judge, solution);
        }

        workflowBuilder = workflowBuilder.AddEdge(solution, confidence);

        // Self-critique back-edge — Confidence routes either through the retry loop
        // (back to the hypothesis branches with augmented context) or forward to the
        // commit gate / report. The retry budget is enforced inside ConfidenceExecutor
        // via AutofixWorkflowConfig.MaxConfidenceRetries so the cycle terminates.
        ExecutorBinding commitTarget;
        if (config.StoppingPointBeforeCommit)
        {
            var gate = new StoppingPointGateExecutor<ConfidenceAudit>(
                "loom.autofix.gate.pre_commit", "pre_commit");
            workflowBuilder = workflowBuilder
                .AddExternalCall<ConfidenceAudit, ConfidenceAudit>(gate, "loom.autofix.review.pre_commit")
                .ForwardMessage<ConfidenceAudit>("loom.autofix.review.pre_commit", [report]);
            commitTarget = gate;
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
