using System.Net;
using System.Text.Json;
using Qyl.Collector.Storage;
using static Qyl.Collector.Tests.CollectorApiTestHost;

namespace Qyl.Collector.Tests;

/// <summary>Contract 11.6.0 annotations over the real HTTP composition.</summary>
public sealed class AnnotationApiTests
{
    [Fact]
    public async Task Create_returns_201_with_location_etag_and_the_annotation()
    {
        await using var host = await StartWithTraceAsync();

        using var response = await host.SendAsync(HttpMethod.Post, "/api/v1/annotations", "writer",
            TraceNote("slow checkout"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await JsonAsync(response);
        var id = body.GetProperty("id").GetString()!;
        Assert.Equal($"/api/v1/annotations/{id}", response.Headers.Location?.OriginalString);
        Assert.Equal(body.GetProperty("etag").GetString(), response.Headers.ETag?.ToString());
        Assert.Equal("trace", body.GetProperty("target").GetProperty("kind").GetString());
        Assert.Equal(TraceId(1), body.GetProperty("target").GetProperty("trace_id").GetString());
        Assert.Equal("slow checkout", body.GetProperty("text").GetString());
        Assert.Equal(body.GetProperty("created_at").GetString(), body.GetProperty("updated_at").GetString());

        using var read = await host.SendAsync(HttpMethod.Get, $"/api/v1/annotations/{id}", "reader");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(response.Headers.ETag, read.Headers.ETag);
        Assert.Equal("slow checkout", (await JsonAsync(read)).GetProperty("text").GetString());
    }

    [Theory]
    [InlineData("span", HttpStatusCode.Created)]
    [InlineData("session", HttpStatusCode.Created)]
    [InlineData("missing-trace", HttpStatusCode.NotFound)]
    [InlineData("missing-span", HttpStatusCode.NotFound)]
    [InlineData("missing-session", HttpStatusCode.NotFound)]
    [InlineData("other-project-trace", HttpStatusCode.NotFound)]
    public async Task A_target_must_exist_in_the_callers_project(string target, HttpStatusCode expected)
    {
        await using var host = await StartWithTraceAsync();
        await host.Store.EnqueueAsync(new SpanBatch([Span(TraceId(9), SpanId(9), 1, 2, project: "beta")]),
            TestContext.Current.CancellationToken);

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

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("""{"target":{"kind":"trace","trace_id":"00000000000000000000000000000001"},"text":""}""", "text")]
    [InlineData("""{"target":{"kind":"trace","trace_id":"NOT-A-TRACE"},"text":"x"}""", "target.trace_id")]
    [InlineData("""{"target":{"kind":"span","trace_id":"00000000000000000000000000000001","span_id":"zz"},"text":"x"}""", "target.span_id")]
    [InlineData("""{"target":{"kind":"session","session_id":".."},"text":"x"}""", "target.session_id")]
    [InlineData("""{"target":{"kind":"metric","name":"x"},"text":"x"}""", "body")]
    [InlineData("""{"text":"x"}""", "body")]
    [InlineData("""not json""", "body")]
    public async Task Invalid_create_bodies_are_400_with_the_offending_field(string json, string field)
    {
        await using var host = await StartWithTraceAsync();

        using var response = await host.SendAsync(HttpMethod.Post, "/api/v1/annotations", "writer", json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(field, (await JsonAsync(response)).GetProperty("errors")[0].GetProperty("field").GetString());
    }

    [Fact]
    public async Task Text_is_bounded_at_2000_code_points()
    {
        await using var host = await StartWithTraceAsync();

        // 2000 astral characters are 4000 UTF-16 units; the contract counts characters.
        using var atLimit = await host.SendAsync(HttpMethod.Post, "/api/v1/annotations", "writer",
            TraceNote(string.Concat(Enumerable.Repeat("\U0001F600", 2000))));
        Assert.Equal(HttpStatusCode.Created, atLimit.StatusCode);

        using var overLimit = await host.SendAsync(HttpMethod.Post, "/api/v1/annotations", "writer",
            TraceNote(new string('a', 2001)));
        Assert.Equal(HttpStatusCode.BadRequest, overLimit.StatusCode);
    }

    [Fact]
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

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        var firstBody = await JsonAsync(first);
        var replayBody = await JsonAsync(replay);
        Assert.Equal(firstBody.GetProperty("id").GetString(), replayBody.GetProperty("id").GetString());
        Assert.Equal(first.Headers.Location, replay.Headers.Location);
        Assert.Equal(first.Headers.ETag, replay.Headers.ETag);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.NotEqual(firstBody.GetProperty("id").GetString(), (await JsonAsync(differentKey)).GetProperty("id").GetString());

        using var list = await host.SendAsync(HttpMethod.Get, $"/api/v1/annotations?trace_id={TraceId(1)}", "reader");
        Assert.Equal(2, (await JsonAsync(list)).GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Patch_and_delete_require_the_current_etag()
    {
        await using var host = await StartWithTraceAsync();
        using var created = await host.SendAsync(HttpMethod.Post, "/api/v1/annotations", "writer", TraceNote("v1"));
        var id = (await JsonAsync(created)).GetProperty("id").GetString()!;
        var path = $"/api/v1/annotations/{id}";
        var etag = created.Headers.ETag!.ToString();

        using var missing = await host.SendAsync(HttpMethod.Patch, path, "writer", """{"text":"v2"}""");
        Assert.Equal(HttpStatusCode.PreconditionRequired, missing.StatusCode);
        Assert.Equal("If-Match", (await JsonAsync(missing)).GetProperty("required_header").GetString());

        using var updated = await host.SendAsync(HttpMethod.Patch, path, "writer", """{"text":"v2"}""",
            IfMatch(etag));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var updatedBody = await JsonAsync(updated);
        Assert.Equal("v2", updatedBody.GetProperty("text").GetString());
        Assert.NotEqual(etag, updated.Headers.ETag!.ToString());
        Assert.Equal(updatedBody.GetProperty("etag").GetString(), updated.Headers.ETag!.ToString());

        using var stale = await host.SendAsync(HttpMethod.Patch, path, "writer", """{"text":"v3"}""", IfMatch(etag));
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal(updated.Headers.ETag!.ToString(), (await JsonAsync(stale)).GetProperty("current_etag").GetString());

        using var staleDelete = await host.SendAsync(HttpMethod.Delete, path, "deleter", headers: IfMatch(etag));
        Assert.Equal(HttpStatusCode.PreconditionFailed, staleDelete.StatusCode);

        // A client that dropped the quotes still names the same version.
        using var deleted = await host.SendAsync(HttpMethod.Delete, path, "deleter",
            headers: IfMatch(updated.Headers.ETag!.Tag.Trim('"')));
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        using var gone = await host.SendAsync(HttpMethod.Get, path, "reader");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        using var goneUpdate = await host.SendAsync(HttpMethod.Patch, path, "writer", """{"text":"v4"}""",
            IfMatch("*"));
        Assert.Equal(HttpStatusCode.NotFound, goneUpdate.StatusCode);
    }

    [Fact]
    public async Task List_filters_by_target_and_pages_newest_first()
    {
        await using var host = await StartWithTraceAsync();
        var spanNote = $$"""{"target":{"kind":"span","trace_id":"{{TraceId(1)}}","span_id":"{{SpanId(2)}}"},"text":"span"}""";
        var sessionNote = """{"target":{"kind":"session","session_id":"sess-1"},"text":"session"}""";
        foreach (var body in new[] { TraceNote("first"), spanNote, sessionNote, TraceNote("last") })
        {
            using var created = await host.SendAsync(HttpMethod.Post, "/api/v1/annotations", "writer", body);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        var texts = new List<string>();
        string? cursor = null;
        do
        {
            using var page = await host.SendAsync(HttpMethod.Get,
                $"/api/v1/annotations?trace_id={TraceId(1)}&limit=1" +
                (cursor is null ? "" : "&cursor=" + Uri.EscapeDataString(cursor)), "reader");
            var body = await JsonAsync(page);
            texts.Add(Assert.Single(body.GetProperty("items").EnumerateArray()).GetProperty("text").GetString()!);
            cursor = body.TryGetProperty("next_cursor", out var next) ? next.GetString() : null;
            Assert.Equal(cursor is not null, body.GetProperty("has_more").GetBoolean());
        } while (cursor is not null);

        // A trace's annotations include its spans' notes; the session note is not on the trace.
        Assert.Equal(["last", "span", "first"], texts);

        using var bySpan = await host.SendAsync(HttpMethod.Get,
            $"/api/v1/annotations?trace_id={TraceId(1)}&span_id={SpanId(2)}", "reader");
        Assert.Equal("span", Assert.Single((await JsonAsync(bySpan)).GetProperty("items").EnumerateArray())
            .GetProperty("text").GetString());
        using var bySession = await host.SendAsync(HttpMethod.Get, "/api/v1/annotations?session_id=sess-1", "reader");
        Assert.Equal("session", Assert.Single((await JsonAsync(bySession)).GetProperty("items").EnumerateArray())
            .GetProperty("text").GetString());
        using var spanWithoutTrace = await host.SendAsync(HttpMethod.Get, $"/api/v1/annotations?span_id={SpanId(2)}", "reader");
        Assert.Equal(HttpStatusCode.BadRequest, spanWithoutTrace.StatusCode);
        using var foreignCursor = await host.SendAsync(HttpMethod.Get,
            "/api/v1/annotations?cursor=" + Uri.EscapeDataString(PageCursorCodecProbe.TracesCursor()), "reader");
        Assert.Equal(HttpStatusCode.BadRequest, foreignCursor.StatusCode);

        // Another project sees none of them.
        using var beta = await host.SendAsync(HttpMethod.Get, "/api/v1/annotations", "beta-all");
        Assert.Equal(0, (await JsonAsync(beta)).GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Another_projects_annotation_reads_as_absent()
    {
        await using var host = await StartWithTraceAsync();
        using var created = await host.SendAsync(HttpMethod.Post, "/api/v1/annotations", "writer", TraceNote("alpha"));
        var id = (await JsonAsync(created)).GetProperty("id").GetString()!;

        using var read = await host.SendAsync(HttpMethod.Get, $"/api/v1/annotations/{id}", "beta-all");
        using var delete = await host.SendAsync(HttpMethod.Delete, $"/api/v1/annotations/{id}", "beta-all",
            headers: IfMatch("*"));

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    private static async Task<CollectorApiTestHost> StartWithTraceAsync()
    {
        var host = await StartAsync(TestContext.Current.CancellationToken);
        await host.Store.EnqueueAsync(new SpanBatch(
        [
            Span(TraceId(1), SpanId(1), 1_000, 5_000, sessionId: "sess-1"),
            Span(TraceId(1), SpanId(2), 2_000, 3_000, parentSpanId: SpanId(1))
        ]), TestContext.Current.CancellationToken);
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
