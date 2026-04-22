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
///     <code>
///     Start -> GatherContext -> Rca -> [fan-out]
///                                        ├─ Impact           (advisory, terminal)
///                                        ├─ IssueTriage      (advisory, terminal)
///                                        └─ SolutionPlan -> DiffGen -> Confidence -> PolicyGate
///     </code>
///     RCA broadcasts its output to three downstream executors via <c>WorkflowBuilder.AddFanOutEdge</c>
///     with no <c>targetSelector</c> — all targets receive every message. Impact and IssueTriage are leaf
///     advisory executors that persist their results to the autofix_steps ledger and terminate; they do not
///     feed back into the main pipeline. The main pipeline continues through SolutionPlan and is the sole
///     source of the workflow output via <see cref="WorkflowBuilder.WithOutputFrom" />.
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
        var impact = ActivatorUtilities.CreateInstance<ImpactExecutor>(services);
        var issueTriage = ActivatorUtilities.CreateInstance<IssueTriageExecutor>(services);
        var solutionPlan = ActivatorUtilities.CreateInstance<SolutionPlanExecutor>(services);
        var diffGen = ActivatorUtilities.CreateInstance<DiffGenExecutor>(services);
        var confidence = ActivatorUtilities.CreateInstance<ConfidenceExecutor>(services);
        var policyGate = ActivatorUtilities.CreateInstance<PolicyGateExecutor>(services);

        return new WorkflowBuilder(start)
            .AddEdge(start, gatherContext)
            .AddEdge(gatherContext, rca)
            .AddFanOutEdge(rca, [impact, issueTriage, solutionPlan])
            .AddEdge(solutionPlan, diffGen)
            .AddEdge(diffGen, confidence)
            .AddEdge(confidence, policyGate)
            .WithOutputFrom(policyGate)
            .WithName("qyl.loom/autofix")
            .Build();
    }
}
