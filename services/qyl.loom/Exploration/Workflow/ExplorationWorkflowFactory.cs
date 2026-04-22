// Copyright (c) 2025-2026 ancplua

using Microsoft.Agents.AI.Workflows;
using Qyl.Loom.Exploration.Workflow.Executors;

namespace Qyl.Loom.Exploration.Workflow;

/// <summary>
///     Builds a fresh exploration <see cref="Microsoft.Agents.AI.Workflows.Workflow" /> per invocation.
///     Graph: <c>BuildContext -> Diagnose -> PlanSolution -> Finalize</c>. Every executor honors
///     <see cref="ExplorationRunState.IsError" /> and <see cref="ExplorationRunState.IsInterrupted" />
///     by passing state through; the terminal <see cref="FinalizeExplorationExecutor" /> is the single
///     point that emits the completion update.
/// </summary>
public static class ExplorationWorkflowFactory
{
    public static Microsoft.Agents.AI.Workflows.Workflow Create(IServiceProvider services)
    {
        var buildContext = ActivatorUtilities.CreateInstance<BuildContextExecutor>(services);
        var diagnose = ActivatorUtilities.CreateInstance<DiagnoseExecutor>(services);
        var planSolution = ActivatorUtilities.CreateInstance<PlanSolutionExecutor>(services);
        var finalize = ActivatorUtilities.CreateInstance<FinalizeExplorationExecutor>(services);

        return new WorkflowBuilder(buildContext)
            .AddEdge(buildContext, diagnose)
            .AddEdge(diagnose, planSolution)
            .AddEdge(planSolution, finalize)
            .WithOutputFrom(finalize)
            .WithName("qyl.loom/exploration")
            .Build();
    }
}
