// Copyright (c) 2025-2026 ancplua

namespace Qyl.Loom.Autofix.Workflow;

internal sealed class AutofixContextLoader(CollectorClient collector, AutofixRunRegistry registry)
{
    public async ValueTask<LoadedContext> LoadAsync(string runId, CancellationToken ct)
    {
        var run = registry.Get(runId)
                  ?? throw new InvalidOperationException($"Run {runId} not registered before workflow start.");

        var issue = await collector.GetIssueByIdAsync(run.IssueId, ct).ConfigureAwait(false);
        var events = await collector.GetIssueEventsAsync(run.IssueId, 5, ct).ConfigureAwait(false);

        var issueBlock = issue is null
            ? $"(issue {run.IssueId} not found)"
            : $"""
               id: {issue.IssueId}
               error type: {issue.ErrorType}
               message: {issue.ErrorMessage ?? "(none)"}
               event count: {issue.EventCount}
               first seen: {issue.FirstSeen:O}
               last seen: {issue.LastSeen:O}
               """;

        var eventLines = events.Count is 0
            ? "(no recent events)"
            : string.Join("\n", events.Select(static e =>
                $"- {e.Timestamp:O} | {e.Environment} | {e.Message ?? "(no message)"}"));

        return new LoadedContext(issueBlock, eventLines, issue);
    }

    internal sealed record LoadedContext(string IssueBlock, string EventsBlock, IssueSummary? Issue);
}

internal sealed class AutofixRunRegistry
{
    private readonly ConcurrentDictionary<string, RegisteredRun> _runs = new(StringComparer.Ordinal);

    public void Register(RegisteredRun run) => _runs[run.RunId] = run;

    public RegisteredRun? Get(string runId) =>
        _runs.TryGetValue(runId, out var run) ? run : null;

    public bool TryRemove(string runId) => _runs.TryRemove(runId, out _);

    internal sealed record RegisteredRun(string RunId, string IssueId, string Policy);
}
