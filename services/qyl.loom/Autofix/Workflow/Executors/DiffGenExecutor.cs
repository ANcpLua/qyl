// Copyright (c) 2025-2026 ancplua

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Qyl.Instrumentation.Instrumentation.GenAi;

namespace Qyl.Loom.Autofix.Workflow.Executors;

/// <summary>
///     Step 4: emits a structured JSON diff (schema-version 1) for the proposed fix.
///     Honors <c>stopping_point = "code_changes"</c>.
/// </summary>
internal sealed class DiffGenExecutor(CollectorClient collector, IChatClient llm)
    : AutofixPipelineExecutor("autofix.diff_gen", stepNumber: 4, stepName: "diff_generation", collector)
{
    protected override async ValueTask<(AutofixRunState State, string OutputJson)> DoWorkAsync(
        AutofixRunState state, CancellationToken cancellationToken)
    {
        var userMessage = $"""
                           Root Cause Analysis:
                           {state.RcaReport}

                           Solution Plan:
                           {state.SolutionPlan}
                           """;

        var agent = llm.AsAIAgent(new ChatClientAgentOptions
        {
            Name = "AutofixDiffGenAgent",
            Description = "Emits a structured JSON diff for the autofix pipeline.",
            ChatOptions = new ChatOptions { Instructions = AutofixPrompts.DiffGeneration },
        }).AsBuilder().UseQylAgentTelemetry().Build();

        var response = await agent.RunAsync(userMessage, cancellationToken: cancellationToken).ConfigureAwait(false);
        var changes = AutofixJson.ExtractObject(response.Text ?? string.Empty);

        var next = state with { ChangesJson = changes };
        if (state.StoppingPoint is "code_changes")
        {
            next = next with { IsEarlyStop = true, EarlyStopReason = "Stopped at code_changes per stopping_point" };
        }

        return (next, changes);
    }
}
