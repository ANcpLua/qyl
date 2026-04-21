// Copyright (c) 2025-2026 ancplua

using Microsoft.Agents.AI.Workflows;
using Qyl.Loom.Autofix.Workflow.Executors;

namespace Qyl.Loom.Autofix.Workflow;

/// <summary>
///     Builds a fresh autofix <see cref="Microsoft.Agents.AI.Workflows.Workflow" /> per fix run. A new graph
///     instance per run keeps executor state isolated — the run id threads through the shared
///     <see cref="AutofixRunState" /> rather than through captured executor fields.
/// </summary>
/// <remarks>
///     Graph:
///     <c>Start -> GatherContext -> Rca -> SolutionPlan -> DiffGen -> Confidence -> PolicyGate</c>.
///     All intermediate executors honor <see cref="AutofixRunState.IsEarlyStop" /> and
///     <see cref="AutofixRunState.IsFatalError" /> by passing state through; the terminal
///     <see cref="PolicyGateExecutor" /> is the single point that transitions the run to its final status.
/// </remarks>
public static class AutofixWorkflowFactory
{
    public static Microsoft.Agents.AI.Workflows.Workflow Create(IServiceProvider services)
    {
        var start = ActivatorUtilities.CreateInstance<StartAutofixExecutor>(services);
        var gatherContext = ActivatorUtilities.CreateInstance<GatherContextExecutor>(services);
        var rca = ActivatorUtilities.CreateInstance<RcaExecutor>(services);
        var solutionPlan = ActivatorUtilities.CreateInstance<SolutionPlanExecutor>(services);
        var diffGen = ActivatorUtilities.CreateInstance<DiffGenExecutor>(services);
        var confidence = ActivatorUtilities.CreateInstance<ConfidenceExecutor>(services);
        var policyGate = ActivatorUtilities.CreateInstance<PolicyGateExecutor>(services);

        return new WorkflowBuilder(start)
            .AddEdge(start, gatherContext)
            .AddEdge(gatherContext, rca)
            .AddEdge(rca, solutionPlan)
            .AddEdge(solutionPlan, diffGen)
            .AddEdge(diffGen, confidence)
            .AddEdge(confidence, policyGate)
            .WithOutputFrom(policyGate)
            .Build();
    }
}
