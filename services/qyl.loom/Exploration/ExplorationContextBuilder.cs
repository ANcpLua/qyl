namespace Qyl.Loom.Exploration;

public sealed class ExplorationContextBuilder(CollectorClient collector)
{
    public const int DefaultMaxStackLength = 800;

    public async Task<ExplorationContext> BuildAsync(
        string issueId,
        string? userContext = null,
        int maxEvents = 5,
        int maxStackLength = DefaultMaxStackLength,
        CancellationToken ct = default)
    {
        var issueTask = collector.GetIssueByIdAsync(issueId, ct);
        var eventsTask = collector.GetIssueEventsAsync(issueId, maxEvents, ct);

        await Task.WhenAll(issueTask, eventsTask).ConfigureAwait(false);

        var issue = await issueTask.ConfigureAwait(false);
        var events = await eventsTask.ConfigureAwait(false);

        if (issue is null) return ExplorationContext.Empty;

        var block = FormatBlock(issue, events, userContext, maxStackLength);
        return new ExplorationContext(issue, events, userContext, block);
    }

    internal static string FormatBlock(
        IssueSummary issue,
        IReadOnlyList<IssueEventDto> events,
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

public sealed record ExplorationContext(
    IssueSummary? Issue,
    IReadOnlyList<IssueEventDto> Events,
    string? UserContext,
    string FormattedBlock)
{
    public static ExplorationContext Empty { get; } = new(null, [], null, string.Empty);
    public bool IsEmpty => Issue is null;
}
