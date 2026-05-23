using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Qyl.Collector.Storage;
using Qyl.Collector.Telemetry;
using Qyl.Collector.Tests.Telemetry;

namespace Qyl.Collector.Tests.Functional;

[Trait("Category", "Functional")]
[Collection(FunctionalCollection.Name)]
public sealed class ConversationMetricsTests
    : IClassFixture<ConversationMetricsTests.CollectorFactory>
{
    private readonly CollectorFactory _factory;

    public ConversationMetricsTests(CollectorFactory factory) => _factory = factory;

    [Fact]
    public async Task Get_conversation_records_dot_named_span_count_metric()
    {
        var ct = TestContext.Current.CancellationToken;
        var sessionId = $"conversation-metric-{Guid.NewGuid():N}";
        await SeedConversationAsync(sessionId, ct);

        using var collector = new InProcessMetricCollector(static instrument =>
            instrument.Meter.Name == QylTelemetry.ConversationsMeterName &&
            instrument.Name == QylTelemetry.ConversationSpanCountMetricName);

        using var client = _factory.CreateClient();
        using var response = await client.GetAsync($"/api/v1/conversations/{sessionId}", ct);

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, responseBody);

        var measurement = collector.Measurements.Should().ContainSingle(static captured =>
            captured.MeterName == QylTelemetry.ConversationsMeterName &&
            captured.Name == QylTelemetry.ConversationSpanCountMetricName).Subject;
        measurement.Unit.Should().Be("{span}");
        measurement.Description.Should().Be("Number of spans returned in a single conversation thread fetch");
        measurement.Value.Should().Be(2d);
        measurement.HasTag("agent_name", "metric-agent").Should().BeTrue();
    }

    private async Task SeedConversationAsync(string sessionId, CancellationToken ct)
    {
        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<DuckDbStore>();
        var start = new DateTimeOffset(2026, 5, 23, 10, 0, 0, TimeSpan.Zero);

        await store.WriteBatchAsync(new SpanBatch([
            CreateConversationSpan(sessionId, "conversation-span-a", start),
            CreateConversationSpan(sessionId, "conversation-span-b", start.AddSeconds(1))
        ]), ct);
    }

    private static SpanStorageRow CreateConversationSpan(
        string sessionId,
        string spanId,
        DateTimeOffset start)
    {
        var startNano = ToUnixNanoseconds(start);
        const ulong durationNano = 1_000_000;

        return new SpanStorageRow
        {
            SpanId = spanId,
            TraceId = $"trace-{sessionId}",
            SessionId = sessionId,
            Name = "chat completion",
            Kind = 1,
            StartTimeUnixNano = startNano,
            EndTimeUnixNano = startNano + durationNano,
            DurationNs = durationNano,
            StatusCode = 1,
            ServiceName = "conversation-metric-test",
            GenAiProviderName = "openai",
            GenAiRequestModel = "gpt-5.5",
            GenAiInputTokens = 11,
            GenAiOutputTokens = 7,
            AttributesJson = """{"gen_ai.agent.name":"metric-agent"}"""
        };
    }

    private static ulong ToUnixNanoseconds(DateTimeOffset value)
    {
        var seconds = value.ToUnixTimeSeconds();
        var ticksWithinSecond = value.Ticks % TimeSpan.TicksPerSecond;
        return (ulong)((seconds * 1_000_000_000L) + (ticksWithinSecond * 100L));
    }

    public sealed class CollectorFactory() : CollectorFunctionalFactory("conversation-metrics")
    {
    }
}
