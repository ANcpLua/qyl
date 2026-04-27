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

        var hypothesis = new HypothesisExecutor(
            "loom.autofix.hypothesis",
            agents.BuildHypothesisStageAgent(),
            config,
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

        var workflowBuilder = new WorkflowBuilder(fixability)
            .AddEdge(fixability, context)
            .AddEdge(context, hypothesis);

        // Stopping point #1 — pre-solution review (HITL).
        if (config.StoppingPointAfterHypothesis)
        {
            var gate = new StoppingPointGateExecutor<HypothesisVerdict>(
                "loom.autofix.gate.pre_solution", "pre_solution");
            workflowBuilder = workflowBuilder
                .AddEdge(hypothesis, gate)
                .AddExternalCall<HypothesisVerdict, HypothesisVerdict>(gate, "loom.autofix.review.pre_solution")
                .ForwardMessage<HypothesisVerdict>("loom.autofix.review.pre_solution", [solution]);
        }
        else
        {
            workflowBuilder = workflowBuilder.AddEdge(hypothesis, solution);
        }

        workflowBuilder = workflowBuilder.AddEdge(solution, confidence);

        // Stopping point #2 — pre-commit review (HITL).
        if (config.StoppingPointBeforeCommit)
        {
            var gate = new StoppingPointGateExecutor<ConfidenceAudit>(
                "loom.autofix.gate.pre_commit", "pre_commit");
            workflowBuilder = workflowBuilder
                .AddEdge(confidence, gate)
                .AddExternalCall<ConfidenceAudit, ConfidenceAudit>(gate, "loom.autofix.review.pre_commit")
                .ForwardMessage<ConfidenceAudit>("loom.autofix.review.pre_commit", [report]);
        }
        else
        {
            workflowBuilder = workflowBuilder.AddEdge(confidence, report);
        }

        return workflowBuilder
            .WithOutputFrom(report)
            .WithName("Qyl.Loom.Autofix.Workflow")
            .Build();
    }
}
