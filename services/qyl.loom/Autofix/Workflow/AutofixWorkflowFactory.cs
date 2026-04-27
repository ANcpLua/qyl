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

        return new WorkflowBuilder(fixability)
            .AddEdge(fixability, context)
            .AddEdge(context, hypothesis)
            .AddEdge(hypothesis, solution)
            .AddEdge(solution, confidence)
            .AddEdge(confidence, report)
            .WithOutputFrom(report)
            .WithName("Qyl.Loom.Autofix.Workflow")
            .Build();
    }
}
