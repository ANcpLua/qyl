// Copyright (c) 2025-2026 ancplua

namespace Qyl.Loom.Autofix.Workflow.Executors;

/// <summary>Step 1: fetch issue events and build the context JSON passed to the RCA agent.</summary>
internal sealed class GatherContextExecutor(CollectorClient collector)
    : AutofixPipelineExecutor("autofix.gather_context", stepNumber: 1, stepName: "gather_context", collector)
{
    protected override async ValueTask<(AutofixRunState State, string OutputJson)> DoWorkAsync(
        AutofixRunState state, CancellationToken cancellationToken)
    {
        var issue = state.Issue!;
        var events = await Collector
            .GetIssueEventsAsync(state.IssueId, 5, cancellationToken)
            .ConfigureAwait(false);

        var context = new
        {
            issue_id = state.IssueId,
            error_type = issue.ErrorType,
            error_message = issue.ErrorMessage,
            event_count = issue.EventCount,
            first_seen = issue.FirstSeen.ToString("O"),
            last_seen = issue.LastSeen.ToString("O"),
            events = events.Select(static e => new
            {
                e.Id,
                e.Message,
                e.StackTrace,
                e.Environment,
                timestamp = e.Timestamp.ToString("O"),
            }),
        };

        var contextJson = JsonSerializer.Serialize(context);
        return (state with { ContextJson = contextJson }, contextJson);
    }
}
