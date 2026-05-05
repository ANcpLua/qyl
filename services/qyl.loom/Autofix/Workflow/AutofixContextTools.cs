
using System.ComponentModel;

namespace Qyl.Loom.Autofix.Workflow;

internal sealed class AutofixContextTools(CollectorClient collector)
{
    [Description("""
                 Fetch the most recent N events for a qyl issue. Returns timestamped event records
                 with environment + message + truncated stack trace. Use to verify the stack-frame
                 chain that produced the error before forming a hypothesis.
                 """)]
    public async Task<string> GetRecentEventsAsync(
        [Description("The qyl issue id to fetch events for.")] string issueId,
        [Description("How many events to fetch (1..50, default 10).")] int limit = 10,
        CancellationToken ct = default)
    {
        var take = Math.Clamp(limit, 1, 50);
        var events = await collector.GetIssueEventsAsync(issueId, take, ct).ConfigureAwait(false);
        if (events.Count is 0) return "(no events found)";

        var sb = new StringBuilder();
        foreach (var e in events)
        {
            sb.AppendLine($"- [{e.Timestamp:O}] env={e.Environment} {e.Message ?? "(no message)"}");
            if (e.StackTrace is not null)
            {
                var truncated = e.StackTrace[..Math.Min(800, e.StackTrace.Length)];
                sb.AppendLine($"  stack:\n{truncated}");
            }
        }
        return sb.ToString();
    }

    [Description("""
                 Look up a qyl issue summary by id — error type, message, event count, first-seen,
                 last-seen, status. Use this once early in context gathering to anchor the
                 hypothesis on the canonical issue record.
                 """)]
    public async Task<string> GetIssueSummaryAsync(
        [Description("The qyl issue id.")] string issueId,
        CancellationToken ct = default)
    {
        var issue = await collector.GetIssueByIdAsync(issueId, ct).ConfigureAwait(false);
        if (issue is null) return $"(issue {issueId} not found)";

        return $"""
                id: {issue.IssueId}
                error_type: {issue.ErrorType}
                message: {issue.ErrorMessage ?? "(none)"}
                event_count: {issue.EventCount}
                first_seen: {issue.FirstSeen:O}
                last_seen: {issue.LastSeen:O}
                status: {issue.Status}
                """;
    }

    [Description("""
                 List recent issues across the project to surface patterns that might correlate
                 with the issue under investigation (e.g. a deployment that introduced a cluster
                 of related errors). Returns up to N issues sorted by last-seen.
                 """)]
    public async Task<string> ListRecentIssuesAsync(
        [Description("How many issues to list (1..25, default 10).")] int limit = 10,
        CancellationToken ct = default)
    {
        var take = Math.Clamp(limit, 1, 25);
        var issues = await collector.GetRecentIssuesAsync(take, ct).ConfigureAwait(false);
        if (issues.Count is 0) return "(no recent issues)";

        var sb = new StringBuilder();
        foreach (var i in issues)
        {
            sb.AppendLine(
                $"- {i.IssueId} | {i.ErrorType} | x{i.EventCount} | last={i.LastSeen:O} | {i.ErrorMessage ?? "(no message)"}");
        }
        return sb.ToString();
    }

    [Description("""
                 Fetch deployments that landed after a specific timestamp. Use to correlate the
                 issue's first-seen with a code deploy — a hypothesis that names a deploy as the
                 trigger needs this evidence.
                 """)]
    public async Task<string> GetDeploymentsAfterAsync(
        [Description("ISO-8601 UTC timestamp; deployments at or after this point are returned.")] string sinceUtc,
        CancellationToken ct = default)
    {
        if (!DateTimeOffset.TryParse(sinceUtc, out var since))
            return $"(invalid timestamp: {sinceUtc})";

        var deployments = await collector.GetDeploymentsAfterAsync(since.UtcDateTime, ct).ConfigureAwait(false);
        if (deployments.Count is 0) return "(no deployments after that timestamp)";

        var sb = new StringBuilder();
        foreach (var d in deployments)
        {
            sb.AppendLine(
                $"- {d.DeploymentId} | service={d.ServiceName} | version={d.ServiceVersion} | status={d.Status} | start={d.StartTime:O}");
        }
        return sb.ToString();
    }
}
