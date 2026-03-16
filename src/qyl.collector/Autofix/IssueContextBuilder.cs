namespace Qyl.Collector.Autofix;

public sealed class IssueContextBuilder(DuckDbStore store, IssueService issueService)
{
    public const int DefaultMaxStackLength = 800;

    public async Task<IssueContext> BuildAsync(
        string issueId,
        string? userContext = null,
        int maxEvents = 5,
        int maxStackLength = DefaultMaxStackLength,
        CancellationToken ct = default)
    {
        var issue = await store.GetIssueByIdAsync(issueId, ct);
        if (issue is null) return IssueContext.Empty;

        var events =
            await issueService.GetEventsAsync(issueId, maxEvents, ct);

        var block = FormatBlock(issue, events, userContext, maxStackLength);
        return new IssueContext(issue, events, userContext, block);
    }

    public async Task<string> GetFormattedContextAsync(
        string issueId, string? userContext = null, CancellationToken ct = default)
    {
        var ctx = await BuildAsync(issueId, userContext, ct: ct);
        return ctx.FormattedBlock;
    }

    internal static string FormatBlock(
        IssueSummary issue,
        IReadOnlyList<ErrorIssueEventRow> events,
        string? userContext,
        int maxStackLength = DefaultMaxStackLength)
    {
        StringBuilder sb = new();
        sb.AppendLine($"Error type: {issue.ErrorType}");
        sb.AppendLine($"Message: {issue.ErrorMessage ?? "N/A"}");
        sb.AppendLine($"Occurrences: {issue.EventCount}");
        sb.AppendLine($"First seen: {issue.FirstSeen:O}");
        sb.AppendLine($"Last seen: {issue.LastSeen:O}");

        if (events.Count > 0)
        {
            sb.AppendLine("\nRecent events:");
            foreach (var e in events)
            {
                sb.AppendLine($"  [{e.Timestamp:O}] {e.Message ?? "no message"}");
                if (e.StackTrace is not null)
                    sb.AppendLine($"    Stack: {e.StackTrace[..Math.Min(maxStackLength, e.StackTrace.Length)]}");
                if (e.Environment is not null)
                    sb.AppendLine($"    Env: {e.Environment}");
            }
        }

        if (userContext is not null)
            sb.AppendLine($"\nAdditional context from user:\n{userContext}");

        return sb.ToString();
    }
}

public sealed record IssueContext(
    IssueSummary? Issue,
    IReadOnlyList<ErrorIssueEventRow> Events,
    string? UserContext,
    string FormattedBlock)
{
    public static IssueContext Empty { get; } = new(null, [], null, string.Empty);
    public bool IsEmpty => Issue is null;
}
