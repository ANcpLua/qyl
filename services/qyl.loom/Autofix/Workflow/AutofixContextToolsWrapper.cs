
using ModelContextProtocol.Server;

namespace Qyl.Loom.Autofix.Workflow;

[McpServerToolType]
public sealed partial class AutofixContextToolsWrapper(CollectorClient collector)
{
    private readonly AutofixContextTools _inner = new(collector);

    [McpServerTool(Name = "autofix_recent_events", Title = "Fetch Recent Issue Events",
        ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    public partial Task<string> GetRecentEventsAsync(
        string issueId,
        int limit = 10,
        CancellationToken ct = default) =>
        _inner.GetRecentEventsAsync(issueId, limit, ct);

    [McpServerTool(Name = "autofix_issue_summary", Title = "Get Issue Summary",
        ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    public partial Task<string> GetIssueSummaryAsync(
        string issueId,
        CancellationToken ct = default) =>
        _inner.GetIssueSummaryAsync(issueId, ct);

    [McpServerTool(Name = "autofix_list_recent_issues", Title = "List Recent Issues",
        ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    public partial Task<string> ListRecentIssuesAsync(
        int limit = 10,
        CancellationToken ct = default) =>
        _inner.ListRecentIssuesAsync(limit, ct);

    [McpServerTool(Name = "autofix_deployments_after", Title = "Get Deployments After Timestamp",
        ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    public partial Task<string> GetDeploymentsAfterAsync(
        string sinceUtc,
        CancellationToken ct = default) =>
        _inner.GetDeploymentsAfterAsync(sinceUtc, ct);
}
