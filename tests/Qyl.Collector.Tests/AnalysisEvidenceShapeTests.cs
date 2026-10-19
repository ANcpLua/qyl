using System.Net;
using System.Text.Json;
using Qyl.Collector.Storage;
using static Qyl.Collector.Tests.CollectorApiTestHost;

namespace Qyl.Collector.Tests;

/// <summary>
/// Evidence names each span once and refers to it by trace and span id: get_failure_context's error
/// spans, propagation and retries, and compare_traces' differences, which say what changed and how.
/// </summary>
public sealed partial class AnalysisApiTests
{
    [TestMethod]
    public async Task Failure_context_lists_each_span_once_and_refers_to_it_by_id()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var time = FixtureUnixNano - 30 * Minute;
        await host.Store.EnqueueAsync(new SpanBatch([
            Span(TraceId(1), SpanId(1), time, time + 50_000_000, status: 1) with { Name = "checkout" },
            Span(TraceId(1), SpanId(2), time + 10, time + 1_000_000, parentSpanId: SpanId(1), service: "payments", status: 2) with { Name = "charge", StatusMessage = "declined" },
            Span(TraceId(1), SpanId(3), time + 2_000_000, time + 3_000_000, parentSpanId: SpanId(1), service: "payments", status: 2) with { Name = "charge", StatusMessage = "declined" },
            Span(TraceId(1), SpanId(4), time + 4_000_000, time + 5_000_000, parentSpanId: SpanId(1), status: 2) with { Name = "render", StatusMessage = "template missing" }
        ]), testContext.CancellationToken);
        var failures = (await Read(host, "list_failures", "{}")).GetProperty("failures").EnumerateArray().ToArray();
        var charge = failures.Single(static f => f.GetProperty("operation").GetString() == "charge").GetProperty("failure_id").GetString();
        var render = failures.Single(static f => f.GetProperty("operation").GetString() == "render").GetProperty("failure_id").GetString();

        var result = await Read(host, "get_failure_context", $$"""{"failure_id":"{{charge}}"}""");

        var observed = result.GetProperty("observed");
        static string Ref(JsonElement span) => span.GetProperty("trace_id").GetString() + "/" + span.GetProperty("span_id").GetString();
        string Id(int span) => TraceId(1) + "/" + SpanId(span);
        Assert.AreEquivalent(new[] { Id(1), Id(2), Id(3), Id(4) }, observed.GetProperty("spans").EnumerateArray().Select(Ref).ToArray());
        Assert.AreEquivalent(new[] { Id(2), Id(3), Id(4) }, observed.GetProperty("error_spans").EnumerateArray().Select(Ref).ToArray());
        Assert.IsTrue(observed.GetProperty("error_spans").EnumerateArray().All(static span => span.EnumerateObject().Count() == 2), "error spans are references");
        Assert.AreEqual(Id(2), Ref(observed.GetProperty("root_cause_span")));
        var retry = Assert.ContainsSingle(observed.GetProperty("retries").EnumerateArray());
        Assert.AreEquivalent(new[] { Id(2), Id(3) }, retry.GetProperty("spans").EnumerateArray().Select(Ref).ToArray());
        Assert.AreEquivalent(new[] { Id(1) + ">" + Id(2), Id(1) + ">" + Id(3), Id(1) + ">" + Id(4) },
            observed.GetProperty("propagation").EnumerateArray().Select(p => Ref(p.GetProperty("from")) + ">" + Ref(p.GetProperty("to"))).ToArray());
        var similar = result.GetProperty("context").GetProperty("similar_failures").EnumerateArray().Select(static f => f.GetProperty("failure_id").GetString()).ToArray();
        Assert.AreSequenceEqual([render], similar);
    }

    [TestMethod]
    public async Task Compare_traces_names_each_difference_by_key_and_values_with_span_ids()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var time = FixtureUnixNano - 30 * Minute;
        await host.Store.EnqueueAsync(new SpanBatch([
            Span(TraceId(1), SpanId(1), time, time + 200_000_000, status: 1) with { Name = "request" },
            Span(TraceId(1), SpanId(2), time + 10, time + 100_000_010, parentSpanId: SpanId(1), status: 2) with { Name = "db", AttributesJson = """{"db.response.status_code":"57014"}""" },
            Span(TraceId(2), SpanId(3), time, time + 200_000_000, status: 1) with { Name = "request" },
            Span(TraceId(2), SpanId(4), time + 10, time + 10_000_010, parentSpanId: SpanId(3), status: 1) with { Name = "db", AttributesJson = """{"db.response.status_code":"00000"}""" }
        ]), testContext.CancellationToken);

        var result = await Read(host, "compare_traces", $$$"""{"a":{"trace_id":"{{{TraceId(1)}}}"},"b":{"trace_id":"{{{TraceId(2)}}}"}}""");

        var pair = result.GetProperty("aligned").EnumerateArray().Single(static p => p.GetProperty("a_span_id").GetString() == SpanId(2));
        Assert.AreEqual(SpanId(4), pair.GetProperty("b_span_id").GetString());
        var differences = pair.GetProperty("differences").EnumerateArray().ToDictionary(static d => d.GetProperty("kind").GetString()!);
        Assert.AreEqual(("status", "error", "ok"), (differences["status"].GetProperty("key").GetString(), differences["status"].GetProperty("a").GetString(), differences["status"].GetProperty("b").GetString()));
        Assert.AreEqual(("db.response.status_code", "57014", "00000"), (differences["attribute"].GetProperty("key").GetString(), differences["attribute"].GetProperty("a").GetString(), differences["attribute"].GetProperty("b").GetString()));
        Assert.AreEqual("duration_ms", differences["duration"].GetProperty("key").GetString());
        var first = result.GetProperty("first_divergence");
        Assert.AreEqual(("status", SpanId(2), SpanId(4)), (first.GetProperty("kind").GetString(), first.GetProperty("a_span_id").GetString(), first.GetProperty("b_span_id").GetString()));
        Assert.IsFalse(result.GetRawText().Contains("\"a_span\"", StringComparison.Ordinal), "differences carry span ids, not span copies");

        using var missing = await host.SendAsync(HttpMethod.Post, "/api/v1/analysis/compare_traces", ReaderApiKey, $$$"""{"a":{"trace_id":"{{{TraceId(1)}}}"},"b":{"trace_id":"{{{TraceId(9)}}}"}}""");
        var error = await host.JsonAsync(missing);
        Assert.AreEqual(HttpStatusCode.NotFound, missing.StatusCode, error.GetRawText());
        StringAssert.Contains(error.GetRawText(), TraceId(9));
    }
}
