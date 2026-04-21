// Copyright (c) 2025-2026 ancplua

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Qyl.Instrumentation.Instrumentation.GenAi;

namespace Qyl.Loom.Autofix.Workflow.Executors;

/// <summary>
///     Step 3: converts the RCA report into a structured JSON solution plan.
///     Honors <c>stopping_point = "solution"</c>.
/// </summary>
internal sealed class SolutionPlanExecutor(CollectorClient collector, IChatClient llm)
    : AutofixPipelineExecutor("autofix.solution_plan", stepNumber: 3, stepName: "solution_planning", collector)
{
    protected override async ValueTask<(AutofixRunState State, string OutputJson)> DoWorkAsync(
        AutofixRunState state, CancellationToken cancellationToken)
    {
        var agent = llm.AsAIAgent(new ChatClientAgentOptions
        {
            Name = "AutofixSolutionPlanAgent",
            Description = "Converts an RCA report into a structured JSON solution plan.",
            ChatOptions = new ChatOptions { Instructions = AutofixPrompts.SolutionPlanning },
        }).AsBuilder().UseQylAgentTelemetry().Build();

        var response = await agent
            .RunAsync(state.RcaReport ?? string.Empty, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var plan = AutofixJson.ExtractObject(response.Text ?? string.Empty);

        var next = state with { SolutionPlan = plan };
        if (state.StoppingPoint is "solution")
        {
            next = next with { IsEarlyStop = true, EarlyStopReason = "Stopped at solution per stopping_point" };
        }

        return (next, plan);
    }
}
