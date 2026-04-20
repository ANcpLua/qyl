namespace Qyl.Collector.Tests.Realtime;

using Collector.Realtime;
using Collector.Storage;
using Qyl.Contracts.Primitives;
using Xunit;

public sealed class LiveLogDeduplicatorTests
{
    [Fact]
    public void ProcessBatch_EmitsFirstLogImmediately_AndSummaryAfterQuietWindow()
    {
        var t0 = new DateTimeOffset(2026, 3, 4, 0, 0, 0, TimeSpan.Zero);
        var deduplicator = new LiveLogDeduplicator(TimeSpan.FromSeconds(5));

        var emitted = deduplicator.ProcessBatch(
        [
            CreateLog("svc.api", "error", "connection failed", t0, 1),
            CreateLog("svc.api", "error", "connection failed", t0.AddSeconds(1), 2)
        ]);

        emitted.Should().ContainSingle();
        emitted[0].IsDuplicateSummary.Should().BeFalse();
        emitted[0].RepeatCount.Should().Be(1);

        var flushed = deduplicator.FlushExpired(t0.AddSeconds(7).UtcDateTime);
        flushed.Should().ContainSingle();
        flushed[0].IsDuplicateSummary.Should().BeTrue();
        flushed[0].RepeatCount.Should().Be(1);
        flushed[0].Log.Body.Should().Be("connection failed");
    }

    [Fact]
    public void ProcessBatch_DeduplicatesAcrossInterleavedMessages()
    {
        var t0 = new DateTimeOffset(2026, 3, 4, 0, 0, 0, TimeSpan.Zero);
        var deduplicator = new LiveLogDeduplicator(TimeSpan.FromSeconds(5));

        var emitted = deduplicator.ProcessBatch(
        [
            CreateLog("svc.api", "warn", "A", t0, 1),
            CreateLog("svc.api", "warn", "B", t0.AddSeconds(1), 2),
            CreateLog("svc.api", "warn", "A", t0.AddSeconds(2), 3)
        ]);

        emitted.Count.Should().Be(2);
        emitted.Select(static x => x.Log.Body ?? string.Empty).ToArray().Should().BeEquivalentTo("A", "B");

        var flushed = deduplicator.FlushExpired(t0.AddSeconds(10).UtcDateTime);
        flushed.Should().ContainSingle();
        flushed[0].IsDuplicateSummary.Should().BeTrue();
        flushed[0].Log.Body.Should().Be("A");
        flushed[0].RepeatCount.Should().Be(1);
    }

    [Fact]
    public void ProcessBatch_ForceFlushes_WhenSuppressedLimitReached()
    {
        var t0 = new DateTimeOffset(2026, 3, 4, 0, 0, 0, TimeSpan.Zero);
        var deduplicator = new LiveLogDeduplicator(TimeSpan.FromSeconds(30), 2);

        var emitted = deduplicator.ProcessBatch(
        [
            CreateLog("svc.api", "info", "steady noise", t0, 1),
            CreateLog("svc.api", "info", "steady noise", t0.AddSeconds(1), 2),
            CreateLog("svc.api", "info", "steady noise", t0.AddSeconds(2), 3),
            CreateLog("svc.api", "info", "steady noise", t0.AddSeconds(3), 4)
        ]);

        emitted.Count.Should().Be(3);

        emitted[0].IsDuplicateSummary.Should().BeFalse();
        emitted[1].IsDuplicateSummary.Should().BeTrue();
        emitted[1].RepeatCount.Should().Be(2);
        emitted[2].IsDuplicateSummary.Should().BeFalse();
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
