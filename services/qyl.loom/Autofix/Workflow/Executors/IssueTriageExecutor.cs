// Copyright (c) 2025-2026 ancplua

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Qyl.Instrumentation.Instrumentation.GenAi;

namespace Qyl.Loom.Autofix.Workflow.Executors;

/// <summary>
///     Fan-out sidecar: identifies the suspect commit and a suggested assignee for the issue based on the
///     RCA output. Advisory only — persists to autofix_steps as step 7. Distinct from
///     <c>TriagePipelineService</c> which does the upstream fixability scoring; this runs <b>inside</b> the
///     autofix workflow, alongside impact assessment.
/// </summary>
internal sealed class IssueTriageExecutor(CollectorClient collector, IChatClient llm)
    : AutofixPipelineExecutor("autofix.issue_triage", stepNumber: 7, stepName: "issue_triage", collector)
{
    protected override async ValueTask<(AutofixRunState State, string OutputJson)> DoWorkAsync(
        AutofixRunState state, CancellationToken cancellationToken)
    {
        var issue = state.Issue!;
        var userMessage = $"""
                           Issue {state.IssueId}
                           Error: {issue.ErrorType}: {issue.ErrorMessage ?? "N/A"}
                           First seen: {issue.FirstSeen:O}

                           Root Cause Analysis:
                           {state.RcaReport}
                           """;

        var agent = llm.AsAIAgent(new ChatClientAgentOptions
        {
            Name = "AutofixIssueTriageAgent",
            Description = "Identifies suspect commit and suggested assignee for a qyl error issue.",
            ChatOptions = new ChatOptions { Instructions = AutofixPrompts.Triage },
        }).AsBuilder().UseQylAgentTelemetry().Build();

        var response = await agent.RunAsync(userMessage, cancellationToken: cancellationToken).ConfigureAwait(false);
        var json = AutofixJson.ExtractObject(response.Text ?? string.Empty);

        var triage = TryDeserialize(json);
        return (state with { Triage = triage }, json);
    }

    private static AutofixTriageInfo? TryDeserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, AutofixJsonContext.Default.AutofixTriageInfo);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
