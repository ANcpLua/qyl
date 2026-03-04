using qyl.collector.Realtime;
using qyl.collector.Storage;
using Qyl.Common;
using Xunit;

namespace qyl.collector.tests.Realtime;

public sealed class LiveLogDeduplicatorTests
{
    [Fact]
    public void ProcessBatch_EmitsFirstLogImmediately_AndSummaryAfterQuietWindow()
    {
        var t0 = DateTimeOffset.Parse("2026-03-04T00:00:00Z");
        var deduplicator = new LiveLogDeduplicator(TimeSpan.FromSeconds(5));

        var emitted = deduplicator.ProcessBatch(
        [
            CreateLog("svc.api", "error", "connection failed", t0, 1),
            CreateLog("svc.api", "error", "connection failed", t0.AddSeconds(1), 2),
        ]);

        Assert.Single(emitted);
        Assert.False(emitted[0].IsDuplicateSummary);
        Assert.Equal(1, emitted[0].RepeatCount);

        var flushed = deduplicator.FlushExpired(t0.AddSeconds(7).UtcDateTime);
        Assert.Single(flushed);
        Assert.True(flushed[0].IsDuplicateSummary);
        Assert.Equal(1, flushed[0].RepeatCount);
        Assert.Equal("connection failed", flushed[0].Log.Body);
    }

    [Fact]
    public void ProcessBatch_DeduplicatesAcrossInterleavedMessages()
    {
        var t0 = DateTimeOffset.Parse("2026-03-04T00:00:00Z");
        var deduplicator = new LiveLogDeduplicator(TimeSpan.FromSeconds(5));

        var emitted = deduplicator.ProcessBatch(
        [
            CreateLog("svc.api", "warn", "A", t0, 1),
            CreateLog("svc.api", "warn", "B", t0.AddSeconds(1), 2),
            CreateLog("svc.api", "warn", "A", t0.AddSeconds(2), 3),
        ]);

        Assert.Equal(2, emitted.Count);
        Assert.Equal(["A", "B"], emitted.Select(static x => x.Log.Body ?? string.Empty).ToArray());

        var flushed = deduplicator.FlushExpired(t0.AddSeconds(10).UtcDateTime);
        Assert.Single(flushed);
        Assert.True(flushed[0].IsDuplicateSummary);
        Assert.Equal("A", flushed[0].Log.Body);
        Assert.Equal(1, flushed[0].RepeatCount);
    }

    [Fact]
    public void ProcessBatch_ForceFlushes_WhenSuppressedLimitReached()
    {
        var t0 = DateTimeOffset.Parse("2026-03-04T00:00:00Z");
        var deduplicator = new LiveLogDeduplicator(TimeSpan.FromSeconds(30), maxSuppressed: 2);

        var emitted = deduplicator.ProcessBatch(
        [
            CreateLog("svc.api", "info", "steady noise", t0, 1),
            CreateLog("svc.api", "info", "steady noise", t0.AddSeconds(1), 2),
            CreateLog("svc.api", "info", "steady noise", t0.AddSeconds(2), 3),
            CreateLog("svc.api", "info", "steady noise", t0.AddSeconds(3), 4),
        ]);

        Assert.Equal(3, emitted.Count);

        Assert.False(emitted[0].IsDuplicateSummary);
        Assert.True(emitted[1].IsDuplicateSummary);
        Assert.Equal(2, emitted[1].RepeatCount);
        Assert.False(emitted[2].IsDuplicateSummary);
    }

    private static LogStorageRow CreateLog(
        string service,
        string severityText,
        string body,
        DateTimeOffset timestamp,
        int id) =>
        new()
        {
            LogId = $"log-{id:D4}",
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
