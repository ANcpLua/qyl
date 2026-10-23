using System.Text.Json;
using Qyl.Api.Contracts.Common;
using Qyl.Api.Contracts.Mcp.Tools;
using Qyl.Collector.Analysis;
using Qyl.Collector.Storage;
using static Qyl.Collector.Tests.CollectorApiTestHost;

namespace Qyl.Collector.Tests;

/// <summary>
/// Attribute values keep their recorded type: a matcher compares typed values, discovery counts them
/// apart, and an int64 reaches the caller without losing digits.
/// </summary>
public sealed partial class AnalysisApiTests
{
    /// <summary>The operand contract keeps numbers numbers: 1 equals the number 1, never the string "1".</summary>
    [TestMethod]
    [DataRow("""{"key":"probe.value","op":"eq","value":1}""", "1")]
    [DataRow("""{"key":"probe.value","op":"eq","value":"1"}""", "2")]
    [DataRow("""{"key":"probe.value","op":"in","values":[1]}""", "1")]
    [DataRow("""{"key":"probe.value","op":"neq","value":1}""", "2")]
    [DataRow("""{"key":"probe.value","op":"eq","value":1.0}""", "1")]
    public async Task Matcher_equality_compares_typed_values(string matcher, string expectedTrace)
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var time = FixtureUnixNano - 30 * Minute;
        await host.Store.EnqueueAsync(new SpanBatch([
            Span(TraceId(1), SpanId(1), time, time + 1) with { AttributesJson = """{"probe.value":{"type":"int","value":"1"}}""" },
            Span(TraceId(2), SpanId(2), time, time + 1) with { AttributesJson = """{"probe.value":"1"}""" }
        ]), testContext.CancellationToken);

        var traces = await Read(host, "list_traces", "{\"matchers\":[" + matcher + "]}");

        Assert.AreEqual(TraceId(int.Parse(expectedTrace, System.Globalization.CultureInfo.InvariantCulture)),
            Assert.ContainsSingle(traces.GetProperty("traces").EnumerateArray()).GetProperty("trace_id").GetString());
    }

    /// <summary>
    /// discover_attributes publishes each value in the lossless attribute shape: an int64 above 2^53
    /// keeps every digit, and the number 1 and the string "1" are two different values.
    /// </summary>
    [TestMethod]
    public async Task Discovered_values_keep_their_type_and_every_int64_digit()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var time = FixtureUnixNano - 30 * Minute;
        await host.Store.EnqueueAsync(new SpanBatch([
            Span(TraceId(1), SpanId(1), time, time + 1) with { AttributesJson = """{"probe.number":{"type":"int","value":"9007199254740993"},"probe.value":{"type":"int","value":"1"}}""" },
            Span(TraceId(2), SpanId(2), time, time + 1) with { AttributesJson = """{"probe.value":"1"}""" }
        ]), testContext.CancellationToken);

        var json = await Read(host, "discover_attributes", """{"signal":"spans","key_prefix":"probe."}""");
        var output = JsonSerializer.Deserialize(json.GetRawText(), global::Qyl.Api.Contracts.ContractTypes.TypeInfo<global::Qyl.Api.Contracts.Mcp.Tools.DiscoverAttributesOutput>())!;

        var number = Assert.ContainsSingle(output.Attributes.Single(static a => a.Key == "probe.number").TopValues);
        var integer = Assert.IsInstanceOfType<AttributeIntValue>(Assert.IsInstanceOfType<AttributeValue.ObjectValue>(number.Value).Value);
        Assert.AreEqual(9_007_199_254_740_993L, integer.Value);
        Assert.AreEqual("9007199254740993", json.GetProperty("attributes").EnumerateArray().Single(static a => a.GetProperty("key").GetString() == "probe.number")
            .GetProperty("top_values")[0].GetProperty("value").GetProperty("value").GetString());
        var values = output.Attributes.Single(static a => a.Key == "probe.value").TopValues;
        Assert.HasCount(2, values);
        Assert.AreEqual(1L, Assert.IsInstanceOfType<AttributeIntValue>(Assert.IsInstanceOfType<AttributeValue.ObjectValue>(values.Single(static v => v.Value is AttributeValue.ObjectValue).Value).Value).Value);
        Assert.AreEqual("1", Assert.IsInstanceOfType<AttributeValue.StringValue>(values.Single(static v => v.Value is AttributeValue.StringValue).Value).Value);
        Assert.IsTrue(values.All(static v => v.Count == 1));
    }
}
