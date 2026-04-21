// Copyright (c) 2025-2026 ancplua

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Qyl.Instrumentation.Instrumentation.GenAi;

namespace Qyl.Loom.Autofix.Workflow.Executors;

/// <summary>
///     Step 2: 5-whys root-cause analysis. Produces a human-readable markdown report.
///     When <see cref="AutofixRunState.StoppingPoint" /> is <c>root_cause</c>, flips the run into
///     early-stop so the pipeline terminates at <c>review</c>.
/// </summary>
internal sealed class RcaExecutor(CollectorClient collector, IChatClient llm)
    : AutofixPipelineExecutor("autofix.rca", stepNumber: 2, stepName: "root_cause_analysis", collector)
{
    protected override async ValueTask<(AutofixRunState State, string OutputJson)> DoWorkAsync(
        AutofixRunState state, CancellationToken cancellationToken)
    {
        var issue = state.Issue!;
        var instructionBlock = state.Instruction is not null
            ? $"\n\nAdditional context from the requester:\n{state.Instruction}"
            : "";

        var userMessage = $"""
                           Investigate this error:
                           Type: {issue.ErrorType}
                           Message: {issue.ErrorMessage ?? "N/A"}
                           Occurrences: {issue.EventCount}

                           Full context:
                           {state.ContextJson}{instructionBlock}
                           """;

        var agent = llm.AsAIAgent(new ChatClientAgentOptions
        {
            Name = "AutofixRcaAgent",
            Description = "Root-cause analysis for a qyl error issue using gathered context.",
            ChatOptions = new ChatOptions { Instructions = AutofixPrompts.RootCauseAnalysis },
        }).AsBuilder().UseQylAgentTelemetry().Build();

        var response = await agent.RunAsync(userMessage, cancellationToken: cancellationToken).ConfigureAwait(false);
        var rca = response.Text is { Length: > 0 } text ? text : string.Empty;

        var next = state with { RcaReport = rca };
        if (state.StoppingPoint is "root_cause")
        {
            next = next with { IsEarlyStop = true, EarlyStopReason = "Stopped at root_cause per stopping_point" };
        }

        return (next, rca);
    }
}
