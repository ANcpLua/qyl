using Qyl.Contracts.Copilot;

namespace Qyl.Collector.Autofix;

/// <summary>
///     Builds a shared formatted issue-context block for Loom and agent integrations.
/// </summary>
public sealed class IssueContextBuilder(DuckDbStore store, IssueService issueService)
    : IIssueContextSource
{
    public const int DefaultMaxEvents = 5;
    public const int DefaultMaxStackLength = 800;

    /// <summary>
    ///     Loads issue data and recent events and returns a formatted context block.
    /// </summary>
    public async Task<IssueContext> BuildAsync(
        string issueId,
        string? userContext = null,
        int maxEvents = DefaultMaxEvents,
        int maxStackLength = DefaultMaxStackLength,
        CancellationToken ct = default)
    {
        IssueSummary? issue = await store.GetIssueByIdAsync(issueId, ct).ConfigureAwait(false);
        if (issue is null)
            return IssueContext.Empty;

        IReadOnlyList<ErrorIssueEventRow> events = await issueService
            .GetEventsAsync(issueId, maxEvents, ct)
            .ConfigureAwait(false);

        return new IssueContext(
            issue,
            events,
            userContext,
            FormatBlock(issue, events, userContext, maxStackLength));
    }

    async Task<string> IIssueContextSource.GetFormattedContextAsync(
        string issueId,
        string? userContext,
        CancellationToken ct)
    {
        var context = await BuildAsync(issueId, userContext, ct: ct).ConfigureAwait(false);
        return context.FormattedBlock;
    }

    internal static string FormatBlock(
        IssueSummary issue,
        IReadOnlyList<ErrorIssueEventRow> events,
        string? userContext,
        int maxStackLength = DefaultMaxStackLength)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Error type: {issue.ErrorType}");
        sb.AppendLine($"Message: {issue.ErrorMessage ?? "N/A"}");
        sb.AppendLine($"Occurrences: {issue.EventCount}");
        sb.AppendLine($"First seen: {issue.FirstSeen:O}");
        sb.AppendLine($"Last seen: {issue.LastSeen:O}");
        sb.AppendLine($"Env: {ResolveEnvironment(events)}");

        if (events.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Recent events:");

            for (var index = 0; index < events.Count; index++)
            {
                var evt = events[index];
                sb.AppendLine($"  [{evt.Timestamp:O}] {evt.Message ?? "no message"}");

                if (!string.IsNullOrWhiteSpace(evt.StackTrace))
                    sb.AppendLine($"    Stack: {Truncate(evt.StackTrace, maxStackLength)}");

                if (!string.IsNullOrWhiteSpace(evt.Environment))
                    sb.AppendLine($"    Env: {evt.Environment}");
            }
        }

        if (!string.IsNullOrWhiteSpace(userContext))
        {
            sb.AppendLine();
            sb.AppendLine("Additional context from user:");
            sb.AppendLine(userContext);
        }

        return sb.ToString();
    }

    private static string ResolveEnvironment(IReadOnlyList<ErrorIssueEventRow> events) =>
        events.FirstOrDefault(static e => !string.IsNullOrWhiteSpace(e.Environment))?.Environment ?? "unknown";

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}

/// <summary>
///     Loaded issue context data and its formatted representation.
/// </summary>
public sealed record IssueContext(
    IssueSummary? Issue,
    IReadOnlyList<ErrorIssueEventRow> Events,
    string? UserContext,
    string FormattedBlock)
{
    public static IssueContext Empty { get; } = new(null, [], null, string.Empty);

    public bool IsEmpty => Issue is null;
}
