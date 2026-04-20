namespace Qyl.Collector.Tests.Query;

using Collector.Query;
using Collector.Storage;
using Qyl.Contracts.Primitives;
using Xunit;

public sealed class LogSummaryServiceTests
{
    [Fact]
    public async Task BuildSummaryAsync_GroupsErrorPatterns_AndDetectsResolution()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = new DuckDbStore(":memory:");
        var service = new LogSummaryService(store, TimeProvider.System);
        var now = TimeProvider.System.GetUtcNow();

        await store.InsertLogsAsync(
        [
            CreateLog("svc.api", "error", "Timeout after 1200 ms for user 42", now.AddMinutes(-2), 1),
            CreateLog("svc.api", "error", "Timeout after 980 ms for user 15", now.AddMinutes(-2).AddSeconds(1), 2),
            CreateLog("svc.api", "warn", "Retry scheduled in 2 seconds", now.AddMinutes(-2).AddSeconds(2), 3),
            CreateLog("svc.api", "info", "Request successfully completed", now.AddMinutes(-2).AddSeconds(3), 4)
        ], ct);

        var summary = await service.BuildSummaryAsync(
            "5m",
            "svc.api",
            null,
            null,
            null,
            ct);

        summary.Window.Should().Be("5m");
        summary.TotalCount.Should().Be(4);
        summary.ErrorCount.Should().Be(2);
        summary.WarningCount.Should().Be(1);
        summary.Cursor.Should().NotBeEmpty();
        summary.Summary.Should().ContainEquivalentOf("logged 4 entries");

        var topIssue = summary.TopIssues.Should().ContainSingle().Which;
        topIssue.Resolved.Should().BeTrue();
        topIssue.Count.Should().Be(2);
        topIssue.Pattern.Should().Contain("<N>");
    }

    [Fact]
    public async Task BuildSummaryAsync_WithCursor_ReturnsOnlyDeltaRows()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = new DuckDbStore(":memory:");
        var service = new LogSummaryService(store, TimeProvider.System);
        var now = TimeProvider.System.GetUtcNow();

        await store.InsertLogsAsync(
        [
            CreateLog("svc.api", "info", "startup complete", now.AddMinutes(-1), 1)
        ], ct);

        var first = await service.BuildSummaryAsync(
            "5m",
            "svc.api",
            null,
            null,
            null,
            ct);

        await store.InsertLogsAsync(
        [
            CreateLog("svc.api", "error", "database connection failed", now.AddSeconds(5), 2)
        ], ct);

        var delta = await service.BuildSummaryAsync(
            "5m",
            "svc.api",
            first.Cursor,
            null,
            null,
            ct);

        delta.TotalCount.Should().Be(1);
        delta.ErrorCount.Should().Be(1);
        delta.WarningCount.Should().Be(0);
        delta.TopIssues.Should().ContainSingle();
    }

    [Fact]
    public async Task WaitForLogAsync_ReturnsMatched_WhenFutureLogAppears()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = new DuckDbStore(":memory:");
        var service = new LogSummaryService(store, TimeProvider.System);

        var waitTask = service.WaitForLogAsync(
            new LogWaitRequest(
                "svc.worker",
                Search: "ready",
                TimeoutSeconds: 3,
                PollIntervalMs: 100),
            ct);

        await Task.Delay(200, ct);
        await store.InsertLogsAsync(
        [
            CreateLog("svc.worker", "info", "worker is ready", TimeProvider.System.GetUtcNow().AddSeconds(1), 9)
        ], ct);

        var result = await waitTask;

        result.Matched.Should().BeTrue();
        result.Log.Should().NotBeNull();
        result.Log.Body.Should().ContainEquivalentOf("ready");
        result.WaitedMs.Should().BeGreaterThanOrEqualTo(0);
        result.PollCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task BuildPatternsAsync_GroupsTemplatesAndAggregatesSeverity()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = new DuckDbStore(":memory:");
        var service = new LogSummaryService(store, TimeProvider.System);
        var now = TimeProvider.System.GetUtcNow();

        await store.InsertLogsAsync(
        [
            CreateLog("svc.api", "error", "Timeout after 1200 ms for user 42", now.AddMinutes(-2), 1),
            CreateLog("svc.api", "error", "Timeout after 980 ms for user 15", now.AddMinutes(-2).AddSeconds(1), 2),
            CreateLog("svc.api", "fatal", "Timeout after 1100 ms for user 88", now.AddMinutes(-2).AddSeconds(2), 3),
            CreateLog("svc.api", "error", "Database unavailable", now.AddMinutes(-2).AddSeconds(3), 4)
        ], ct);

        var patterns = await service.BuildPatternsAsync(
            "5m",
            "svc.api",
            null,
            null,
            2,
            17,
            null,
            ct);

        var pattern = patterns.Should().ContainSingle().Which;
        pattern.Count.Should().Be(3);
        pattern.ServiceName.Should().Be("svc.api");
        pattern.Template.Should().Contain("<N>");
        pattern.PatternId.Should().NotBeEmpty();
        pattern.SeverityDistribution.Should().Contain(static x => x.Severity == "error" && x.Count == 2);
        pattern.SeverityDistribution.Should().Contain(static x => x.Severity == "fatal" && x.Count == 1);
    }

    [Fact]
    public async Task BuildPatternsAsync_RespectsExplicitTimeRange()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = new DuckDbStore(":memory:");
        var service = new LogSummaryService(store, TimeProvider.System);
        var now = TimeProvider.System.GetUtcNow();

        var older = now.AddMinutes(-20);
        var recent = now.AddMinutes(-2);

        await store.InsertLogsAsync(
        [
            CreateLog("svc.api", "error", "HTTP 500 for request 1", older, 1),
            CreateLog("svc.api", "error", "HTTP 500 for request 2", recent, 2)
        ], ct);

        var patterns = await service.BuildPatternsAsync(
            "5m",
            "svc.api",
            now.AddMinutes(-5),
            now,
            1,
            17,
            null,
            ct);

        var pattern = patterns.Should().ContainSingle().Which;
        pattern.Count.Should().Be(1);
        pattern.FirstSeen.Should().BeOnOrAfter(now.AddMinutes(-5).UtcDateTime);
    }

    [Fact]
    public async Task BuildStatsAsync_ReturnsSeverityBucketsAndTimeBounds()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = new DuckDbStore(":memory:");
        var service = new LogSummaryService(store, TimeProvider.System);
        var now = TimeProvider.System.GetUtcNow();

        await store.InsertLogsAsync(
        [
            CreateLog("svc.api", "trace", "trace message", now.AddMinutes(-4), 1),
            CreateLog("svc.api", "info", "info message", now.AddMinutes(-3), 2),
            CreateLog("svc.api", "warn", "warn message", now.AddMinutes(-2), 3),
            CreateLog("svc.api", "error", "error message", now.AddMinutes(-1), 4),
            CreateLog("svc.api", "fatal", "fatal message", now.AddSeconds(-30), 5)
        ], ct);

        var stats = await service.BuildStatsAsync(
            "5m",
            "svc.api",
            null,
            null,
            null,
            null,
            ct);

        var bySeverity =
            stats.BySeverity.ToDictionary(static x => x.Severity, static x => x.Count, StringComparer.Ordinal);
        stats.Window.Should().Be("5m");
        stats.TotalCount.Should().Be(5);
        bySeverity["trace"].Should().Be(1);
        bySeverity["debug"].Should().Be(0);
        bySeverity["info"].Should().Be(1);
        bySeverity["warn"].Should().Be(1);
        bySeverity["error"].Should().Be(1);
        bySeverity["fatal"].Should().Be(1);
        stats.OldestTimestamp.Should().NotBeNull();
        stats.NewestTimestamp.Should().NotBeNull();
        stats.NewestTimestamp.Should().BeOnOrAfter(stats.OldestTimestamp.Value);
    }

    [Fact]
    public async Task BuildStatsAsync_RespectsFiltersAndExplicitRange()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = new DuckDbStore(":memory:");
        var service = new LogSummaryService(store, TimeProvider.System);
        var now = TimeProvider.System.GetUtcNow();

        await store.InsertLogsAsync(
        [
            CreateLog("svc.api", "error", "timeout at gateway", now.AddMinutes(-3), 1),
            CreateLog("svc.api", "info", "timeout recovered", now.AddMinutes(-3).AddSeconds(1), 2),
            CreateLog("svc.worker", "error", "timeout at worker", now.AddMinutes(-3).AddSeconds(2), 3),
            CreateLog("svc.api", "error", "timeout old event", now.AddMinutes(-20), 4)
        ], ct);

        var stats = await service.BuildStatsAsync(
            "5m",
            "svc.api",
            now.AddMinutes(-5),
            now,
            17,
            "timeout",
            ct);

        var bySeverity =
            stats.BySeverity.ToDictionary(static x => x.Severity, static x => x.Count, StringComparer.Ordinal);
        stats.Window.Should().Be("custom");
        stats.TotalCount.Should().Be(1);
        bySeverity["error"].Should().Be(1);
        bySeverity["fatal"].Should().Be(0);
    }

    private static LogStorageRow CreateLog(
        string service,
        string severityText,
        string body,
        DateTimeOffset timestamp,
        int id) =>
        new()
        {
            LogId = $"sum-{id:D4}",
            TraceId = null,
            SpanId = null,
            SessionId = null,
            TimeUnixNano = TimeConversions.ToUnixNanoUnsigned(timestamp),
            ObservedTimeUnixNano = null,
            SeverityNumber = ToSeverityNumber(severityText),
            SeverityText = severityText,
            Body = body,
            ServiceName = service,
            AttributesJson = "{}",
            ResourceJson = "{}",
            SourceFile = null,
            SourceLine = null,
            SourceColumn = null,
            SourceMethod = null,
            CreatedAt = null
        };

    private static byte ToSeverityNumber(string severityText) =>
        severityText.ToLowerInvariant() switch
        {
            "trace" => 1,
            "debug" => 5,
            "info" => 9,
            "warn" => 13,
            "error" => 17,
            "fatal" => 21,
            _ => 0
        };
}
