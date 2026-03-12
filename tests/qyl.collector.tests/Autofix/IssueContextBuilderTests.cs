using Qyl.Collector.Autofix;
using Qyl.Collector.Errors;
using Qyl.Collector.Storage;
using Xunit;

namespace Qyl.Collector.Tests.Autofix;

public sealed class IssueContextBuilderTests
{
    [Fact]
    public void FormatBlock_includes_error_type_and_message()
    {
        var issue = MakeIssue("NullReferenceException", "Object reference not set");
        string block = IssueContextBuilder.FormatBlock(issue, [], null);

        Assert.Contains("Error type: NullReferenceException", block);
        Assert.Contains("Message: Object reference not set", block);
    }

    [Fact]
    public void FormatBlock_truncates_stack_at_maxStackLength()
    {
        var issue = MakeIssue("Error", "msg");
        string longStack = new('X', 1000);
        var events = new[] { MakeEvent(longStack) };

        string block800 = IssueContextBuilder.FormatBlock(issue, events, null, maxStackLength: 800);
        string block200 = IssueContextBuilder.FormatBlock(issue, events, null, maxStackLength: 200);

        Assert.DoesNotContain(longStack, block800);
        Assert.DoesNotContain(longStack, block200);
        Assert.True(block200.Length < block800.Length);
    }

    [Fact]
    public void FormatBlock_includes_user_context_when_provided()
    {
        var issue = MakeIssue("Error", "msg");
        string block = IssueContextBuilder.FormatBlock(issue, [], "We saw this after deploy");

        Assert.Contains("We saw this after deploy", block);
    }

    [Fact]
    public void FormatBlock_omits_user_context_when_null()
    {
        var issue = MakeIssue("Error", "msg");
        string block = IssueContextBuilder.FormatBlock(issue, [], null);

        Assert.DoesNotContain("Additional context", block);
    }

    [Fact]
    public void FormatBlock_includes_environment_from_events()
    {
        var issue = MakeIssue("Error", "msg");
        var events = new[] { MakeEvent("stack", environment: "production") };
        string block = IssueContextBuilder.FormatBlock(issue, events, null);

        Assert.Contains("Env: production", block);
    }

    private static IssueSummary MakeIssue(string errorType, string message) => new()
    {
        IssueId = "test-issue",
        Fingerprint = "fp-test",
        ErrorType = errorType,
        ErrorMessage = message,
        Status = IssueStatus.New,
        EventCount = 42,
        FirstSeen = DateTime.UnixEpoch,
        LastSeen = DateTime.UnixEpoch
    };

    private static ErrorIssueEventRow MakeEvent(
        string? stackTrace = null,
        string? environment = null) => new()
    {
        Id = "evt-1",
        IssueId = "test-issue",
        Timestamp = DateTime.UnixEpoch,
        StackTrace = stackTrace,
        Environment = environment
    };
}
