using System.Text.Json;
using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests;

public sealed class AiDiagnosticSpanEventPersistenceTests
{
    private static readonly string[] s_fixedAttributeKeys =
    [
        "qyl.agent.diagnostic.check.count",
        "qyl.agent.diagnostic.check.failed_count",
        "qyl.agent.diagnostic.extension.id",
        "qyl.agent.diagnostic.format.version",
        "qyl.agent.diagnostic.outcome",
        "qyl.agent.diagnostic.phase",
        "qyl.agent.diagnostic.probe.id",
        "qyl.agent.diagnostic.snapshot.id",
        "qyl.agent.diagnostic.variable.count",
        "qyl.workflow.agent.id",
        "qyl.workflow.attempt.id",
        "qyl.workflow.event.id",
        "qyl.workflow.run.id",
        "qyl.workflow.tool_call.id"
    ];

    [Fact]
    public async Task Fixed_diagnostic_projection_persists_on_span_events_without_dynamic_or_sensitive_payloads()
    {
        var diagnosticEvent = new Span.Types.Event
        {
            Name = "qyl.agent.diagnostic.snapshot",
            TimeUnixNano = 2
        };
        diagnosticEvent.Attributes.Add(
        [
            StringAttribute("qyl.agent.diagnostic.extension.id", "qyl.agent.diagnostic.snapshot"),
            IntAttribute("qyl.agent.diagnostic.format.version", 1),
            StringAttribute("qyl.agent.diagnostic.snapshot.id", "snapshot-1"),
            StringAttribute("qyl.agent.diagnostic.probe.id", "probe-1"),
            StringAttribute("qyl.agent.diagnostic.phase", "checkpoint"),
            StringAttribute("qyl.agent.diagnostic.outcome", "fail"),
            IntAttribute("qyl.agent.diagnostic.variable.count", 6),
            IntAttribute("qyl.agent.diagnostic.check.count", 4),
            IntAttribute("qyl.agent.diagnostic.check.failed_count", 1),
            StringAttribute("qyl.workflow.run.id", "run-1"),
            StringAttribute("qyl.workflow.event.id", "event-1"),
            StringAttribute("qyl.workflow.attempt.id", "attempt-1"),
            StringAttribute("qyl.workflow.agent.id", "agent-1"),
            StringAttribute("qyl.workflow.tool_call.id", "tool-call-1"),
            StringAttribute("qyl.agent.diagnostic.variable.connection_string", "dynamic-variable-value"),
            StringAttribute("qyl.agent.diagnostic.variable.0.value", "dynamic-variable-payload"),
            StringAttribute("qyl.agent.diagnostic.check.0.result", "dynamic-check-result"),
            StringAttribute("qyl.agent.diagnostic.snapshot.secret", "secret-payload"),
            StringAttribute("baggage.qyl.agent.diagnostic.snapshot.id", "baggage-payload")
        ]);

        var span = new Span
        {
            TraceId = ByteString.CopyFrom(new byte[16]),
            SpanId = ByteString.CopyFrom(new byte[8]),
            Name = "codex.workflow.turn",
            StartTimeUnixNano = 1,
            EndTimeUnixNano = 3,
            Events = { diagnosticEvent }
        };
        var request = new ExportTraceServiceRequest
        {
            ResourceSpans =
            {
                new ResourceSpans
                {
                    Resource = new Resource(),
                    ScopeSpans = { new ScopeSpans { Spans = { span } } }
                }
            }
        };

        var rows = IngestionStorageMapper.ToSpanStorageRows(OtlpConverter.ConvertTraceRequest(request));
        await using var store = new DuckDbStore(":memory:");
        await store.EnqueueAsync(new SpanBatch(rows), TestContext.Current.CancellationToken);

        var storedSpan = Assert.Single(await store.GetSpansAsync(
            "default",
            ct: TestContext.Current.CancellationToken));
        var storedEvent = Assert.Single(Assert.IsType<List<SpanEventJson>>(
            SpanChildStorage.DeserializeEvents(storedSpan.EventsJson)));
        Assert.Equal("qyl.agent.diagnostic.snapshot", storedEvent.Name);
        var attributesJson = Assert.IsType<string>(storedEvent.AttributesJson);
        using var attributes = JsonDocument.Parse(attributesJson);

        Assert.Equal(
            s_fixedAttributeKeys,
            attributes.RootElement.EnumerateObject().Select(static property => property.Name).ToArray());
        Assert.Equal(
            "qyl.agent.diagnostic.snapshot",
            attributes.RootElement.GetProperty("qyl.agent.diagnostic.extension.id").GetString());
        Assert.Equal(
            "1",
            attributes.RootElement.GetProperty("qyl.agent.diagnostic.format.version")
                .GetProperty("value").GetString());
        Assert.DoesNotContain("dynamic-variable", attributesJson, StringComparison.Ordinal);
        Assert.DoesNotContain("dynamic-check", attributesJson, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-payload", attributesJson, StringComparison.Ordinal);
        Assert.DoesNotContain("baggage-payload", attributesJson, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("qyl.agent.diagnostic.variable.connection_string")]
    [InlineData("qyl.agent.diagnostic.variable.0.value")]
    [InlineData("qyl.agent.diagnostic.check.0.result")]
    [InlineData("qyl.agent.diagnostic.snapshot.secret")]
    [InlineData("baggage.qyl.agent.diagnostic.snapshot.id")]
    public void Dynamic_and_sensitive_diagnostic_keys_are_not_captured(string key)
    {
        Assert.False(AttributeKeySets.ShouldCaptureSpanAttribute(key));
        Assert.False(AttributeKeySets.IsSafeSpanAttribute(key));
    }

    private static KeyValue StringAttribute(string key, string value) =>
        new() { Key = key, Value = new AnyValue { StringValue = value } };

    private static KeyValue IntAttribute(string key, long value) =>
        new() { Key = key, Value = new AnyValue { IntValue = value } };
}
