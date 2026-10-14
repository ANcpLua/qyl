using System.Text.Json;
using Google.Protobuf;
using OpenTelemetry.Proto.Common.V1;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Storage;
using Qyl.Telemetry.SemanticConventions.Names;
using OtlpSpan = OpenTelemetry.Proto.Trace.V1.Span;
using static Qyl.Collector.Tests.OtlpTestSpans;

namespace Qyl.Collector.Tests;

/// <summary>
/// <c>SetAppTag("order.id", v)</c> writes <c>qyl.app.order.id</c>, the one sanctioned place for a business
/// key. Ingest captures the prefix within its bounds: at most 16 keys per span, event and link; a string of at
/// most 256 characters, a 64-bit integer, a double or a bool; no key containing a token the registry denies, and
/// no key whose part after the prefix the registry denies.
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

    /// <summary>
    /// <c>SetAppTag</c> names the key, so the registry's key and prefix denials apply after the prefix too:
    /// <c>qyl.app.user.email</c> is as personal as <c>user.email</c>.
    /// </summary>
    [Theory]
    [InlineData("qyl.app.user.email")]
    [InlineData("qyl.app.session.id")]
    [InlineData("qyl.app.host.name")]
    [InlineData("qyl.app.url.full")]
    public async Task A_key_the_registry_denies_after_the_prefix_is_dropped(string key)
    {
        var attributes = await PersistedAttributesAsync(
            String("qyl.app.order.id", "ord-1042"),
            String(key, "alice@example.com"));

        Assert.Equal(["qyl.app.order.id"], AppKeys(attributes));
    }

    /// <summary>The span's keys leave an event's or a link's 16 untouched, and the 17th key on the child is dropped.</summary>
    [Theory]
    [InlineData("event")]
    [InlineData("link")]
    public async Task Each_event_and_link_keeps_sixteen_app_keys_of_its_own(string carrier)
    {
        var (span, child) = await PersistedAppKeysAsync(carrier, spanAttributes: Fields(17), childAttributes: Fields(17));

        Assert.Equal(FieldNames(16), span);
        Assert.Equal(FieldNames(16), child);
    }

    /// <summary>A key ingest drops (denied, too long, not a scalar) is not counted: the 16 slots go to keys that are kept.</summary>
    [Theory]
    [InlineData("span")]
    [InlineData("event")]
    [InlineData("link")]
    public async Task A_dropped_app_key_does_not_use_up_one_of_the_sixteen_slots(string carrier)
    {
        KeyValue[] attributes =
        [
            String("qyl.app.user.email", "alice@example.com"),
            String("qyl.app.order.message", "leave it at the door"),
            String("qyl.app.note", new string('x', 257)),
            new KeyValue { Key = "qyl.app.tags", Value = new AnyValue { ArrayValue = new ArrayValue { Values = { new AnyValue { StringValue = "a" } } } } },
            .. Fields(17)
        ];

        var (span, child) = carrier is "span"
            ? await PersistedAppKeysAsync(carrier, spanAttributes: attributes, childAttributes: [])
            : await PersistedAppKeysAsync(carrier, spanAttributes: [], childAttributes: attributes);

        Assert.Equal(FieldNames(16), carrier is "span" ? span : child);
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

    private static KeyValue[] Fields(int count) => [.. FieldNames(count).Select(static name => String(name, "v"))];

    private static string[] FieldNames(int count) => [.. Enumerable.Range(1, count).Select(static i => $"qyl.app.field{i:00}")];

    private static async Task<(string[] Span, string[] Child)> PersistedAppKeysAsync(
        string carrier,
        KeyValue[] spanAttributes,
        KeyValue[] childAttributes)
    {
        var request = Request(QylTelemetryNames.Scopes.QylTelemetryApplication, spanAttributes);
        var span = request.ResourceSpans[0].ScopeSpans[0].Spans[0];
        if (carrier is "event")
        {
            var spanEvent = new OtlpSpan.Types.Event { Name = "checkpoint", TimeUnixNano = span.StartTimeUnixNano };
            spanEvent.Attributes.Add(childAttributes);
            span.Events.Add(spanEvent);
        }
        else if (carrier is "link")
        {
            var link = new OtlpSpan.Types.Link
            {
                TraceId = ByteString.CopyFrom([.. Enumerable.Range(17, 16).Select(static i => (byte)i)]),
                SpanId = ByteString.CopyFrom([.. Enumerable.Range(33, 8).Select(static i => (byte)i)])
            };
            link.Attributes.Add(childAttributes);
            span.Links.Add(link);
        }

        await using var host = await CollectorApiTestHost.StartAsync(TestContext.Current.CancellationToken);
        await host.IngestTracesAsync(request);
        var stored = Assert.Single(await host.Store.GetSpansAsync("alpha", ct: TestContext.Current.CancellationToken));

        var childJson = carrier switch
        {
            "event" => Assert.Single(SpanChildStorage.DeserializeEvents(stored.EventsJson)!).AttributesJson,
            "link" => Assert.Single(SpanChildStorage.DeserializeLinks(stored.LinksJson)!).AttributesJson,
            _ => null
        };
        return (AppKeys(stored.AttributesJson), AppKeys(childJson));
    }

    private static string[] AppKeys(string? json)
    {
        using var document = JsonDocument.Parse(json ?? "{}");
        return AppKeys(document.RootElement);
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
