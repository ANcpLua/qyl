using Qyl.Collector.Autofix;
using Qyl.Collector.Errors;
using Qyl.Collector.Storage;
using Xunit;

namespace Qyl.Collector.Tests.Autofix;

public sealed class IssueContextBuilderTests
{
    [Fact]
    public void FormatBlock_UsesCanonicalExplorerStyle()
    {
        var stackTrace = new string('x', IssueContextBuilder.DefaultMaxStackLength + 25);
        var issue = new IssueSummary
        {
            IssueId = "issue-1",
            Fingerprint = "fingerprint-1",
            ErrorType = "NullReferenceException",
            ErrorMessage = "Object reference not set.",
            Status = IssueStatus.New,
            EventCount = 3,
            FirstSeen = new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc),
            LastSeen = new DateTime(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc)
        };
        ErrorIssueEventRow[] events =
        [
            new()
            {
                Id = "evt-1",
                IssueId = "issue-1",
                Message = "Null reference in handler",
                StackTrace = stackTrace,
                Environment = "prod-eu",
                Timestamp = new DateTime(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc)
            }
        ];

        var formatted = IssueContextBuilder.FormatBlock(issue, events, "User saw this after deploy.");

        Assert.Contains("Error type: NullReferenceException", formatted);
        Assert.Contains("Env: prod-eu", formatted);
        Assert.Contains("Additional context from user:", formatted);
        Assert.Contains("User saw this after deploy.", formatted);
        Assert.Contains(new string('x', IssueContextBuilder.DefaultMaxStackLength), formatted);
        Assert.DoesNotContain(new string('x', IssueContextBuilder.DefaultMaxStackLength + 1), formatted);
    }
}
