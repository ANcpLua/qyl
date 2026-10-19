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
}
