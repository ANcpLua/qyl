using Qyl.Collector.Autofix;
using Xunit;

namespace Qyl.Collector.Tests.Autofix;

public sealed class LoomSessionStoreTests
{
    [Fact]
    public void GetOrCreate_returns_same_session_for_same_issue()
    {
        var sut = new LoomSessionStore(TimeProvider.System);

        var first = sut.GetOrCreate("issue-1");
        var second = sut.GetOrCreate("issue-1");

        Assert.Same(first, second);
        Assert.Equal("issue-1", first.SessionId);
        Assert.Equal("issue-1", first.IssueId);
    }

    [Fact]
    public void SaveDiagnosis_persists_root_cause_and_transcript()
    {
        var sut = new LoomSessionStore(TimeProvider.System);
        var session = sut.GetOrCreate("issue-2");
        LoomRootCause rootCause = new()
        {
            Summary = "Controller passes a null dependency into the handler.",
            Steps =
            [
                new LoomCausalStep(1, "Request enters the controller", false),
                new LoomCausalStep(2, "The handler dependency is null", true)
            ]
        };

        sut.SaveDiagnosis(session.SessionId, "diagnostic transcript", rootCause);

        Assert.Equal("diagnostic transcript", session.DiagnosticTranscript);
        Assert.Same(rootCause, session.RootCause);
        Assert.Single(session.Messages);
        Assert.Equal(LoomSessionMessageRole.Diagnostician, session.Messages[0].Role);
    }

    [Fact]
    public void SaveSolution_persists_structured_plan()
    {
        var sut = new LoomSessionStore(TimeProvider.System);
        var session = sut.GetOrCreate("issue-3");
        LoomSolution solution = new()
        {
            Summary = "Inject the missing dependency before executing the handler.",
            Steps =
            [
                new LoomSolutionStep("Register dependency", "Add the missing service registration."),
                new LoomSolutionStep("Guard null path", "Fail fast if resolution still returns null.")
            ]
        };

        sut.SaveSolution(session.SessionId, solution);

        Assert.Same(solution, session.Solution);
        Assert.Single(session.Messages);
        Assert.Equal(LoomSessionMessageRole.Strategist, session.Messages[0].Role);
    }
}
