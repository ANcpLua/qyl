// Copyright (c) 2025-2026 ancplua

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Qyl.Instrumentation.Instrumentation.GenAi;

namespace Qyl.Loom.Autofix.Workflow.Executors;

/// <summary>
///     Fan-out sidecar: runs the impact-assessment prompt against the RCA output and persists the result
///     as an autofix_steps row. Advisory only — downstream executors do not read <c>state.Impact</c>; the
///     dashboard surfaces it alongside the other steps. Augments the issue record with a blast-radius
///     analysis (services, user-visible impact, severity) so operators can triage without re-running RCA.
/// </summary>
internal sealed class ImpactExecutor(CollectorClient collector, IChatClient llm)
    : AutofixPipelineExecutor("autofix.impact", stepNumber: 6, stepName: "impact_assessment", collector)
{
    protected override async ValueTask<(AutofixRunState State, string OutputJson)> DoWorkAsync(
        AutofixRunState state, CancellationToken cancellationToken)
    {
        var issue = state.Issue!;
        var userMessage = $"""
                           Error: {issue.ErrorType}: {issue.ErrorMessage ?? "N/A"}
                           Occurrences: {issue.EventCount}
                           First seen: {issue.FirstSeen:O}
                           Last seen: {issue.LastSeen:O}

                           Root Cause Analysis:
                           {state.RcaReport}
                           """;

        var agent = llm.AsAIAgent(new ChatClientAgentOptions
        {
            Name = "AutofixImpactAgent",
            Description = "Assesses the impact of an error across system, user, and business dimensions.",
            ChatOptions = new ChatOptions { Instructions = AutofixPrompts.ImpactAssessment },
        }).AsBuilder().UseQylAgentTelemetry().Build();

        var response = await agent.RunAsync(userMessage, cancellationToken: cancellationToken).ConfigureAwait(false);
        var json = AutofixJson.ExtractObject(response.Text ?? string.Empty);

        var impact = TryDeserialize(json);
        return (state with { Impact = impact }, json);
    }

    private static ImpactAssessmentResult? TryDeserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, AutofixJsonContext.Default.ImpactAssessmentResult);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
