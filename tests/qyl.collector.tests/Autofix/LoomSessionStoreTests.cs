using Qyl.Collector.Autofix;
using Qyl.Collector.Storage;
using Qyl.Contracts.Autofix;
using Xunit;

namespace Qyl.Collector.Tests.Autofix;

public sealed class LoomSessionStoreTests
{
    [Fact]
    public async Task CreateAsync_returns_session_with_defaults()
    {
        await using var db = new DuckDbStore(":memory:");
        var sut = new LoomSessionStore(db, TimeProvider.System);
        var ct = TestContext.Current.CancellationToken;

        var session = await sut.CreateAsync("issue-1", ct: ct);

        Assert.StartsWith("loom-", session.SessionId);
        Assert.Equal("issue-1", session.IssueId);
        Assert.Equal(LoomSessionMode.Interactive, session.Mode);
        Assert.Equal(LoomStage.Idle, session.Stage);
        Assert.Equal(LoomStatus.Idle, session.Status);
        Assert.True(session.CreatedAt > 0);
    }

    [Fact]
    public async Task GetAsync_returns_null_for_unknown_session()
    {
        await using var db = new DuckDbStore(":memory:");
        var sut = new LoomSessionStore(db, TimeProvider.System);
        var ct = TestContext.Current.CancellationToken;

        var result = await sut.GetAsync("loom-doesnotexist", ct);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_persists_stage_and_status()
    {
        await using var db = new DuckDbStore(":memory:");
        var sut = new LoomSessionStore(db, TimeProvider.System);
        var ct = TestContext.Current.CancellationToken;

        var session = await sut.CreateAsync("issue-2", ct: ct);
        session.Stage = LoomStage.RootCause;
        session.Status = LoomStatus.Completed;
        await sut.UpdateAsync(session, ct);

        var reloaded = await sut.GetAsync(session.SessionId, ct);

        Assert.NotNull(reloaded);
        Assert.Equal(LoomStage.RootCause, reloaded.Stage);
        Assert.Equal(LoomStatus.Completed, reloaded.Status);
    }

    [Fact]
    public async Task GetPendingHandoffsAsync_returns_completed_background_sessions()
    {
        await using var db = new DuckDbStore(":memory:");
        var sut = new LoomSessionStore(db, TimeProvider.System);
        var ct = TestContext.Current.CancellationToken;

        // Background + completed + stage >= RootCause → should appear
        var bg = await sut.CreateAsync("issue-3", LoomSessionMode.Background, ct);
        bg.Stage = LoomStage.RootCause;
        bg.Status = LoomStatus.Completed;
        await sut.UpdateAsync(bg, ct);

        // Interactive + completed → should NOT appear
        var interactive = await sut.CreateAsync("issue-3", LoomSessionMode.Interactive, ct);
        interactive.Stage = LoomStage.RootCause;
        interactive.Status = LoomStatus.Completed;
        await sut.UpdateAsync(interactive, ct);

        var handoffs = await sut.GetPendingHandoffsAsync(ct);

        Assert.Single(handoffs);
        Assert.Equal(bg.SessionId, handoffs[0].SessionId);
    }

    [Fact]
    public async Task AppendMessageAsync_and_GetMessagesAsync_round_trip()
    {
        await using var db = new DuckDbStore(":memory:");
        var sut = new LoomSessionStore(db, TimeProvider.System);
        var ct = TestContext.Current.CancellationToken;

        var session = await sut.CreateAsync("issue-4", ct: ct);
        await sut.AppendMessageAsync(session.SessionId, new LoomMessage(LoomMessageRole.User, "hello"), ct);
        await sut.AppendMessageAsync(session.SessionId, new LoomMessage(LoomMessageRole.Assistant, "world"), ct);

        var messages = await sut.GetMessagesAsync(session.SessionId, ct);

        Assert.Equal(2, messages.Count);
        Assert.Equal(LoomMessageRole.User, messages[0].Role);
        Assert.Equal("hello", messages[0].Content);
        Assert.Equal(LoomMessageRole.Assistant, messages[1].Role);
        Assert.Equal("world", messages[1].Content);
    }
}
