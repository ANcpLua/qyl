using System.Text.Json;
using OpenTelemetry.Proto.Common.V1;
using Qyl.Collector.Ingestion;
using Qyl.Telemetry.SemanticConventions.Names;
using static Qyl.Collector.Tests.OtlpTestSpans;

namespace Qyl.Collector.Tests;

/// <summary>
/// <c>SetAppTag("order.id", v)</c> writes <c>qyl.app.order.id</c>, the one sanctioned place for a business
/// key. Ingest captures the prefix within its bounds: at most 16 keys per span; a string of at most 256
/// characters, a 64-bit integer, a double or a bool; no key containing a token the registry denies.
/// </summary>
public sealed class AppAttributePrefixTests
{
    [Fact]
    public async Task A_business_key_is_persisted_with_each_of_the_four_value_types()
    {
        var attributes = await PersistedAttributesAsync(
            String("qyl.app.order.id", "ord-1042"),
            Int("qyl.app.order.items", 3),
            Double("qyl.app.order.total", 42.5),
            Bool("qyl.app.order.express", true));

        Assert.Equal(Json(OtlpAttributeValue.FromString("ord-1042")), attributes.GetProperty("qyl.app.order.id").GetRawText());
        Assert.Equal(Json(OtlpAttributeValue.FromInt(3)), attributes.GetProperty("qyl.app.order.items").GetRawText());
        Assert.Equal(Json(OtlpAttributeValue.FromDouble(42.5)), attributes.GetProperty("qyl.app.order.total").GetRawText());
        Assert.Equal(Json(OtlpAttributeValue.FromBool(true)), attributes.GetProperty("qyl.app.order.express").GetRawText());
    }

    [Fact]
    public async Task A_key_with_a_denied_token_is_dropped()
    {
        var attributes = await PersistedAttributesAsync(
            String("qyl.app.order.id", "ord-1042"),
            String("qyl.app.order.message", "leave it at the door"));

        Assert.True(attributes.TryGetProperty("qyl.app.order.id", out _));
        Assert.False(attributes.TryGetProperty("qyl.app.order.message", out _));
    }

    [Fact]
    public async Task The_seventeenth_key_is_dropped()
    {
        var attributes = await PersistedAttributesAsync(
            [.. Enumerable.Range(1, 17).Select(static i => String($"qyl.app.field{i:00}", "v"))]);

        var kept = AppKeys(attributes);
        Assert.Equal(16, kept.Length);
        Assert.DoesNotContain("qyl.app.field17", kept);
    }

    [Fact]
    public async Task A_value_longer_than_256_characters_is_dropped()
    {
        var attributes = await PersistedAttributesAsync(
            String("qyl.app.note.short", new string('x', 256)),
            String("qyl.app.note.long", new string('x', 257)),
            new KeyValue { Key = "qyl.app.tags", Value = new AnyValue { ArrayValue = new ArrayValue { Values = { new AnyValue { StringValue = "a" } } } } });

        Assert.Equal(256, attributes.GetProperty("qyl.app.note.short").GetString()!.Length);
        Assert.Equal(["qyl.app.note.short"], AppKeys(attributes));
    }

    /// <summary>
    /// SemConv 10.0.0 adds the <c>qyl.instrumentation.domain</c> values <c>app</c>, <c>messaging.process</c>,
    /// <c>gen_ai.invoke_agent</c> and <c>gen_ai.execute_tool</c>. The collector allowlists the key and keeps any
    /// value; no reader switches on it, so a span from <c>Spans.Work</c> arrives with its domain.
    /// </summary>
    [Fact]
    public async Task An_application_span_keeps_its_instrumentation_domain()
    {
        var attributes = await PersistedAttributesAsync(String("qyl.instrumentation.domain", "app"));

        Assert.Equal("app", attributes.GetProperty("qyl.instrumentation.domain").GetString());
    }

    private static string[] AppKeys(JsonElement attributes) =>
    [
        .. attributes.EnumerateObject()
            .Select(static property => property.Name)
            .Where(static name => name.StartsWith("qyl.app.", StringComparison.Ordinal))
    ];

    private static string Json(OtlpAttributeValue value) =>
        JsonSerializer.Serialize(value.ToContract(), OtlpAttributeJsonContext.Default.AttributeValue);

    private static async Task<JsonElement> PersistedAttributesAsync(params KeyValue[] attributes)
    {
        await using var host = await CollectorApiTestHost.StartAsync(TestContext.Current.CancellationToken);
        await host.IngestTracesAsync(Request(QylTelemetryNames.Scopes.QylTelemetryApplication, attributes));

        var stored = Assert.Single(await host.Store.GetSpansAsync("alpha", ct: TestContext.Current.CancellationToken));
        using var document = JsonDocument.Parse(stored.AttributesJson ?? "{}");
        return document.RootElement.Clone();
    }
}
