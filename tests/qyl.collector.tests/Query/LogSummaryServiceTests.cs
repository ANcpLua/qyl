using qyl.collector.Logs;
using qyl.collector.Storage;
using Qyl.Common;
using Xunit;

namespace qyl.collector.tests.Logs;

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
            sinceCursor: null,
            minSeverity: null,
            search: null,
            ct);

        Assert.Equal("5m", summary.Window);
        Assert.Equal(4, summary.TotalCount);
        Assert.Equal(2, summary.ErrorCount);
        Assert.Equal(1, summary.WarningCount);
        Assert.NotEmpty(summary.Cursor);
        Assert.Contains("logged 4 entries", summary.Summary, StringComparison.OrdinalIgnoreCase);

        var topIssue = Assert.Single(summary.TopIssues);
        Assert.True(topIssue.Resolved);
        Assert.Equal(2, topIssue.Count);
        Assert.Contains("<N>", topIssue.Pattern, StringComparison.Ordinal);
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
            sinceCursor: null,
            minSeverity: null,
            search: null,
            ct);

        await store.InsertLogsAsync(
        [
            CreateLog("svc.api", "error", "database connection failed", now.AddSeconds(5), 2)
        ], ct);

        var delta = await service.BuildSummaryAsync(
            "5m",
            "svc.api",
            sinceCursor: first.Cursor,
            minSeverity: null,
            search: null,
            ct);

        Assert.Equal(1, delta.TotalCount);
        Assert.Equal(1, delta.ErrorCount);
        Assert.Equal(0, delta.WarningCount);
        Assert.Single(delta.TopIssues);
    }

    [Fact]
    public async Task WaitForLogAsync_ReturnsMatched_WhenFutureLogAppears()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = new DuckDbStore(":memory:");
        var service = new LogSummaryService(store, TimeProvider.System);

        var waitTask = service.WaitForLogAsync(
            new LogWaitRequest(
                ServiceName: "svc.worker",
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

        Assert.True(result.Matched);
        Assert.NotNull(result.Log);
        Assert.Contains("ready", result.Log!.Body, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.WaitedMs >= 0);
        Assert.True(result.PollCount > 0);
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
            startTime: null,
            endTime: null,
            minCount: 2,
            minSeverity: 17,
            search: null,
            ct);

        var pattern = Assert.Single(patterns);
        Assert.Equal(3, pattern.Count);
        Assert.Equal("svc.api", pattern.ServiceName);
        Assert.Contains("<N>", pattern.Template, StringComparison.Ordinal);
        Assert.NotEmpty(pattern.PatternId);
        Assert.Contains(pattern.SeverityDistribution, static x => x.Severity == "error" && x.Count == 2);
        Assert.Contains(pattern.SeverityDistribution, static x => x.Severity == "fatal" && x.Count == 1);
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
            startTime: now.AddMinutes(-5),
            endTime: now,
            minCount: 1,
            minSeverity: 17,
            search: null,
            ct);

        var pattern = Assert.Single(patterns);
        Assert.Equal(1, pattern.Count);
        Assert.True(pattern.FirstSeen >= now.AddMinutes(-5).UtcDateTime);
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
            startTime: null,
            endTime: null,
            minSeverity: null,
            search: null,
            ct);

        var bySeverity = stats.BySeverity.ToDictionary(static x => x.Severity, static x => x.Count, StringComparer.Ordinal);
        Assert.Equal("5m", stats.Window);
        Assert.Equal(5, stats.TotalCount);
        Assert.Equal(1, bySeverity["trace"]);
        Assert.Equal(0, bySeverity["debug"]);
        Assert.Equal(1, bySeverity["info"]);
        Assert.Equal(1, bySeverity["warn"]);
        Assert.Equal(1, bySeverity["error"]);
        Assert.Equal(1, bySeverity["fatal"]);
        Assert.NotNull(stats.OldestTimestamp);
        Assert.NotNull(stats.NewestTimestamp);
        Assert.True(stats.NewestTimestamp >= stats.OldestTimestamp);
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
            startTime: now.AddMinutes(-5),
            endTime: now,
            minSeverity: 17,
            search: "timeout",
            ct);

        var bySeverity = stats.BySeverity.ToDictionary(static x => x.Severity, static x => x.Count, StringComparer.Ordinal);
        Assert.Equal("custom", stats.Window);
        Assert.Equal(1, stats.TotalCount);
        Assert.Equal(1, bySeverity["error"]);
        Assert.Equal(0, bySeverity["fatal"]);
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
