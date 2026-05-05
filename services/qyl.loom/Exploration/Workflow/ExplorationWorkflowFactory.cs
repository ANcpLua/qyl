
using Microsoft.Agents.AI.Workflows;
using Qyl.Loom.Exploration.Workflow.Executors;

namespace Qyl.Loom.Exploration.Workflow;

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
