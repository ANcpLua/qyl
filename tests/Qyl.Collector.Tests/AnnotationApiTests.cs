using System.Net;
using System.Text.Json;
using Qyl.Collector.Storage;
using static Qyl.Collector.Tests.CollectorApiTestHost;

namespace Qyl.Collector.Tests;

/// <summary>Contract 11.6.0 annotations over the real HTTP composition.</summary>
[TestClass]
public sealed class AnnotationApiTests(TestContext testContext)
{
    [TestMethod]
    public async Task Create_returns_201_with_location_etag_and_the_annotation()
    {
        await using var host = await StartWithTraceAsync();

        using var response = await host.SendAsync(HttpMethod.Post, "/api/v1/annotations", "writer",
            TraceNote("slow checkout"));

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        var body = await host.JsonAsync(response);
        var id = body.GetProperty("id").GetString()!;
        Assert.AreEqual($"/api/v1/annotations/{id}", response.Headers.Location?.OriginalString);
        Assert.AreEqual(body.GetProperty("etag").GetString(), response.Headers.ETag?.ToString());
        Assert.AreEqual("trace", body.GetProperty("target").GetProperty("kind").GetString());
        Assert.AreEqual(TraceId(1), body.GetProperty("target").GetProperty("trace_id").GetString());
        Assert.AreEqual("slow checkout", body.GetProperty("text").GetString());
        Assert.AreEqual(body.GetProperty("created_at").GetString(), body.GetProperty("updated_at").GetString());

        using var read = await host.SendAsync(HttpMethod.Get, $"/api/v1/annotations/{id}", "reader");
        Assert.AreEqual(HttpStatusCode.OK, read.StatusCode);
        Assert.AreEqual(response.Headers.ETag, read.Headers.ETag);
        Assert.AreEqual("slow checkout", (await host.JsonAsync(read)).GetProperty("text").GetString());
    }

    [TestMethod]
    [DataRow("span", HttpStatusCode.Created)]
    [DataRow("session", HttpStatusCode.Created)]
    [DataRow("missing-trace", HttpStatusCode.NotFound)]
    [DataRow("missing-span", HttpStatusCode.NotFound)]
    [DataRow("missing-session", HttpStatusCode.NotFound)]
    [DataRow("other-project-trace", HttpStatusCode.NotFound)]
    public async Task A_target_must_exist_in_the_callers_project(string target, HttpStatusCode expected)
    {
        await using var host = await StartWithTraceAsync();
        await host.Store.EnqueueAsync(new SpanBatch([Span(TraceId(9), SpanId(9), 1, 2, project: "beta")]),
            testContext.CancellationToken);

        var targetJson = target switch
        {
            "span" => $$"""{"kind":"span","trace_id":"{{TraceId(1)}}","span_id":"{{SpanId(2)}}"}""",
            "session" => """{"kind":"session","session_id":"sess-1"}""",
            "missing-trace" => $$"""{"kind":"trace","trace_id":"{{TraceId(7)}}"}""",
            "missing-span" => $$"""{"kind":"span","trace_id":"{{TraceId(1)}}","span_id":"{{SpanId(7)}}"}""",
            "missing-session" => """{"kind":"session","session_id":"sess-unknown"}""",
            _ => $$"""{"kind":"trace","trace_id":"{{TraceId(9)}}"}"""
        };
        using var response = await host.SendAsync(HttpMethod.Post, "/api/v1/annotations", "writer",
            $$"""{"target":{{targetJson}},"text":"note"}""");

        Assert.AreEqual(expected, response.StatusCode);
    }

    [TestMethod]
    [DataRow("""{"target":{"kind":"trace","trace_id":"00000000000000000000000000000001"},"text":""}""", "text")]
    [DataRow("""{"target":{"kind":"trace","trace_id":"NOT-A-TRACE"},"text":"x"}""", "target.trace_id")]
    [DataRow("""{"target":{"kind":"span","trace_id":"00000000000000000000000000000001","span_id":"zz"},"text":"x"}""", "target.span_id")]
    [DataRow("""{"target":{"kind":"session","session_id":".."},"text":"x"}""", "target.session_id")]
    [DataRow("""{"target":{"kind":"metric","name":"x"},"text":"x"}""", "body")]
    [DataRow("""{"text":"x"}""", "body")]
    [DataRow("""not json""", "body")]
    public async Task Invalid_create_bodies_are_400_with_the_offending_field(string json, string field)
    {
        await using var host = await StartWithTraceAsync();

        using var response = await host.SendAsync(HttpMethod.Post, "/api/v1/annotations", "writer", json);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.AreEqual("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.AreEqual(field, (await host.JsonAsync(response)).GetProperty("errors")[0].GetProperty("field").GetString());
    }

    [TestMethod]
    public async Task Text_is_bounded_at_2000_code_points()
    {
        await using var host = await StartWithTraceAsync();

        // 2000 astral characters are 4000 UTF-16 units; the contract counts characters.
        using var atLimit = await host.SendAsync(HttpMethod.Post, "/api/v1/annotations", "writer",
            TraceNote(string.Concat(Enumerable.Repeat("\U0001F600", 2000))));
        Assert.AreEqual(HttpStatusCode.Created, atLimit.StatusCode);

        using var overLimit = await host.SendAsync(HttpMethod.Post, "/api/v1/annotations", "writer",
            TraceNote(new string('a', 2001)));
        Assert.AreEqual(HttpStatusCode.BadRequest, overLimit.StatusCode);
    }

    [TestMethod]
    public async Task Idempotency_key_replays_the_first_result_and_refuses_a_different_body()
    {
        await using var host = await StartWithTraceAsync();
        var key = new Dictionary<string, string> { ["Idempotency-Key"] = "retry-1" };

        using var first = await host.SendAsync(HttpMethod.Post, "/api/v1/annotations", "writer",
            TraceNote("once"), key);
        // The same body with other whitespace and member order is the same request.
        using var replay = await host.SendAsync(HttpMethod.Post, "/api/v1/annotations", "writer",
            $$"""{ "text" : "once", "target" : { "trace_id" : "{{TraceId(1)}}", "kind" : "trace" } }""", key);
        using var conflict = await host.SendAsync(HttpMethod.Post, "/api/v1/annotations", "writer",
            TraceNote("twice"), key);
        using var differentKey = await host.SendAsync(HttpMethod.Post, "/api/v1/annotations", "writer",
            TraceNote("once"), new Dictionary<string, string> { ["Idempotency-Key"] = "retry-2" });

        Assert.AreEqual(HttpStatusCode.Created, first.StatusCode);
        Assert.AreEqual(HttpStatusCode.Created, replay.StatusCode);
        var firstBody = await host.JsonAsync(first);
        var replayBody = await host.JsonAsync(replay);
        Assert.AreEqual(firstBody.GetProperty("id").GetString(), replayBody.GetProperty("id").GetString());
        Assert.AreEqual(first.Headers.Location, replay.Headers.Location);
        Assert.AreEqual(first.Headers.ETag, replay.Headers.ETag);
        Assert.AreEqual(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.AreNotEqual(firstBody.GetProperty("id").GetString(), (await host.JsonAsync(differentKey)).GetProperty("id").GetString());

        using var list = await host.SendAsync(HttpMethod.Get, $"/api/v1/annotations?trace_id={TraceId(1)}", "reader");
        Assert.AreEqual(2, (await host.JsonAsync(list)).GetProperty("items").GetArrayLength());
    }

    [TestMethod]
    public async Task Patch_and_delete_require_the_current_etag()
    {
        await using var host = await StartWithTraceAsync();
        using var created = await host.SendAsync(HttpMethod.Post, "/api/v1/annotations", "writer", TraceNote("v1"));
        var id = (await host.JsonAsync(created)).GetProperty("id").GetString()!;
        var path = $"/api/v1/annotations/{id}";
        var etag = created.Headers.ETag!.ToString();

        using var missing = await host.SendAsync(HttpMethod.Patch, path, "writer", """{"text":"v2"}""");
        Assert.AreEqual(HttpStatusCode.PreconditionRequired, missing.StatusCode);
        Assert.AreEqual("If-Match", (await host.JsonAsync(missing)).GetProperty("required_header").GetString());

        using var updated = await host.SendAsync(HttpMethod.Patch, path, "writer", """{"text":"v2"}""",
            IfMatch(etag));
        Assert.AreEqual(HttpStatusCode.OK, updated.StatusCode);
        var updatedBody = await host.JsonAsync(updated);
        Assert.AreEqual("v2", updatedBody.GetProperty("text").GetString());
        Assert.AreNotEqual(etag, updated.Headers.ETag!.ToString());
        Assert.AreEqual(updatedBody.GetProperty("etag").GetString(), updated.Headers.ETag!.ToString());

        using var stale = await host.SendAsync(HttpMethod.Patch, path, "writer", """{"text":"v3"}""", IfMatch(etag));
        Assert.AreEqual(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.AreEqual(updated.Headers.ETag!.ToString(), (await host.JsonAsync(stale)).GetProperty("current_etag").GetString());

        using var staleDelete = await host.SendAsync(HttpMethod.Delete, path, "deleter", headers: IfMatch(etag));
        Assert.AreEqual(HttpStatusCode.PreconditionFailed, staleDelete.StatusCode);

        // A client that dropped the quotes still names the same version.
        using var deleted = await host.SendAsync(HttpMethod.Delete, path, "deleter",
            headers: IfMatch(updated.Headers.ETag!.Tag.Trim('"')));
        Assert.AreEqual(HttpStatusCode.NoContent, deleted.StatusCode);

        using var gone = await host.SendAsync(HttpMethod.Get, path, "reader");
        Assert.AreEqual(HttpStatusCode.NotFound, gone.StatusCode);
        using var goneUpdate = await host.SendAsync(HttpMethod.Patch, path, "writer", """{"text":"v4"}""",
            IfMatch("*"));
        Assert.AreEqual(HttpStatusCode.NotFound, goneUpdate.StatusCode);
    }

    [TestMethod]
    public async Task List_filters_by_target_and_pages_newest_first()
    {
        await using var host = await StartWithTraceAsync();
        var spanNote = $$"""{"target":{"kind":"span","trace_id":"{{TraceId(1)}}","span_id":"{{SpanId(2)}}"},"text":"span"}""";
        var sessionNote = """{"target":{"kind":"session","session_id":"sess-1"},"text":"session"}""";
        foreach (var body in new[] { TraceNote("first"), spanNote, sessionNote, TraceNote("last") })
        {
            using var created = await host.SendAsync(HttpMethod.Post, "/api/v1/annotations", "writer", body);
            Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
        }

        var texts = new List<string>();
        string? cursor = null;
        do
        {
            using var page = await host.SendAsync(HttpMethod.Get,
                $"/api/v1/annotations?trace_id={TraceId(1)}&limit=1" +
                (cursor is null ? "" : "&cursor=" + Uri.EscapeDataString(cursor)), "reader");
            var body = await host.JsonAsync(page);
            texts.Add(Assert.ContainsSingle(body.GetProperty("items").EnumerateArray()).GetProperty("text").GetString()!);
            cursor = body.TryGetProperty("next_cursor", out var next) ? next.GetString() : null;
            Assert.AreEqual(cursor is not null, body.GetProperty("has_more").GetBoolean());
        } while (cursor is not null);

        // A trace's annotations include its spans' notes; the session note is not on the trace.
        Assert.AreSequenceEqual(["last", "span", "first"], texts);

        using var bySpan = await host.SendAsync(HttpMethod.Get,
            $"/api/v1/annotations?trace_id={TraceId(1)}&span_id={SpanId(2)}", "reader");
        Assert.AreEqual("span", Assert.ContainsSingle((await host.JsonAsync(bySpan)).GetProperty("items").EnumerateArray())
            .GetProperty("text").GetString());
        using var bySession = await host.SendAsync(HttpMethod.Get, "/api/v1/annotations?session_id=sess-1", "reader");
        Assert.AreEqual("session", Assert.ContainsSingle((await host.JsonAsync(bySession)).GetProperty("items").EnumerateArray())
            .GetProperty("text").GetString());
        using var spanWithoutTrace = await host.SendAsync(HttpMethod.Get, $"/api/v1/annotations?span_id={SpanId(2)}", "reader");
        Assert.AreEqual(HttpStatusCode.BadRequest, spanWithoutTrace.StatusCode);
        using var foreignCursor = await host.SendAsync(HttpMethod.Get,
            "/api/v1/annotations?cursor=" + Uri.EscapeDataString(PageCursorCodecProbe.TracesCursor()), "reader");
        Assert.AreEqual(HttpStatusCode.BadRequest, foreignCursor.StatusCode);

        // Another project sees none of them.
        using var beta = await host.SendAsync(HttpMethod.Get, "/api/v1/annotations", "beta-all");
        Assert.AreEqual(0, (await host.JsonAsync(beta)).GetProperty("items").GetArrayLength());
    }

    [TestMethod]
    public async Task Another_projects_annotation_reads_as_absent()
    {
        await using var host = await StartWithTraceAsync();
        using var created = await host.SendAsync(HttpMethod.Post, "/api/v1/annotations", "writer", TraceNote("alpha"));
        var id = (await host.JsonAsync(created)).GetProperty("id").GetString()!;

        using var read = await host.SendAsync(HttpMethod.Get, $"/api/v1/annotations/{id}", "beta-all");
        using var delete = await host.SendAsync(HttpMethod.Delete, $"/api/v1/annotations/{id}", "beta-all",
            headers: IfMatch("*"));

        Assert.AreEqual(HttpStatusCode.NotFound, read.StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, delete.StatusCode);
    }

    private async Task<CollectorApiTestHost> StartWithTraceAsync()
    {
        var host = await StartAsync(testContext.CancellationToken);
        await host.Store.EnqueueAsync(new SpanBatch(
        [
            Span(TraceId(1), SpanId(1), 1_000, 5_000, sessionId: "sess-1"),
            Span(TraceId(1), SpanId(2), 2_000, 3_000, parentSpanId: SpanId(1))
        ]), testContext.CancellationToken);
        return host;
    }

    private static string TraceNote(string text) =>
        $$"""{"target":{"kind":"trace","trace_id":"{{TraceId(1)}}"},"text":{{JsonSerializer.Serialize(text, QylSerializerContext.Default.String)}}}""";

    private static Dictionary<string, string> IfMatch(string etag) => new() { ["If-Match"] = etag };
}

internal static class PageCursorCodecProbe
{
    public static string TracesCursor() =>
        Hosting.PageCursorCodec.Encode(
            Hosting.PageCursorKind.Traces,
            new TracePageCursor(1, TraceId(1)));
}
