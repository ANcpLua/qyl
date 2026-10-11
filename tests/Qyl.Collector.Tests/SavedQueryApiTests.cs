using System.Net;
using OpenTelemetry.Proto.Collector.Metrics.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Metrics.V1;
using OpenTelemetry.Proto.Resource.V1;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Primitives;
using Qyl.Collector.Storage;
using static Qyl.Collector.Tests.CollectorApiTestHost;
using OtlpMetric = OpenTelemetry.Proto.Metrics.V1.Metric;

namespace Qyl.Collector.Tests;

/// <summary>Contract 11.6.0 saved queries, their write base and <c>:run</c>, over the real HTTP composition.</summary>
public sealed class SavedQueryApiTests
{
    private const string ErrorsFilter = """{"signal":"traces","lookback_minutes":60,"service_name":"checkout","errors_only":true}""";

    [Fact]
    public async Task Create_get_patch_delete_round_trip_with_etags()
    {
        await using var host = await StartAsync(TestContext.Current.CancellationToken);

        using var created = await host.SendAsync(HttpMethod.Post, "/api/v1/saved-queries", "writer",
            $$"""{"name":"checkout errors","description":"last hour","filter":{{ErrorsFilter}}}""");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await JsonAsync(created);
        var id = body.GetProperty("id").GetString()!;
        Assert.Equal($"/api/v1/saved-queries/{id}", created.Headers.Location?.OriginalString);
        Assert.Equal("traces", body.GetProperty("filter").GetProperty("signal").GetString());
        Assert.True(body.GetProperty("filter").GetProperty("errors_only").GetBoolean());

        using var read = await host.SendAsync(HttpMethod.Get, $"/api/v1/saved-queries/{id}", "reader");
        Assert.Equal(created.Headers.ETag, read.Headers.ETag);

        using var renamed = await host.SendAsync(HttpMethod.Patch, $"/api/v1/saved-queries/{id}", "writer",
            """{"name":"renamed"}""", IfMatch(created.Headers.ETag!.ToString()));
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        var renamedBody = await JsonAsync(renamed);
        // Omitted members keep their value.
        Assert.Equal("renamed", renamedBody.GetProperty("name").GetString());
        Assert.Equal("last hour", renamedBody.GetProperty("description").GetString());
        Assert.Equal("checkout", renamedBody.GetProperty("filter").GetProperty("service_name").GetString());

        using var refiltered = await host.SendAsync(HttpMethod.Patch, $"/api/v1/saved-queries/{id}", "writer",
            """{"filter":{"signal":"logs","severity_min":17}}""", IfMatch(renamed.Headers.ETag!.ToString()));
        Assert.Equal(HttpStatusCode.OK, refiltered.StatusCode);
        Assert.Equal("logs", (await JsonAsync(refiltered)).GetProperty("filter").GetProperty("signal").GetString());

        using var listLogs = await host.SendAsync(HttpMethod.Get, "/api/v1/saved-queries?signal=logs", "reader");
        Assert.Single((await JsonAsync(listLogs)).GetProperty("items").EnumerateArray());
        using var listTraces = await host.SendAsync(HttpMethod.Get, "/api/v1/saved-queries?signal=traces", "reader");
        Assert.Empty((await JsonAsync(listTraces)).GetProperty("items").EnumerateArray());

        using var stale = await host.SendAsync(HttpMethod.Delete, $"/api/v1/saved-queries/{id}", "deleter",
            headers: IfMatch(created.Headers.ETag!.ToString()));
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        using var deleted = await host.SendAsync(HttpMethod.Delete, $"/api/v1/saved-queries/{id}", "deleter",
            headers: IfMatch(refiltered.Headers.ETag!.ToString()));
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var gone = await host.SendAsync(HttpMethod.Get, $"/api/v1/saved-queries/{id}", "reader");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Fact]
    public async Task Names_are_unique_per_project_on_create_and_rename()
    {
        await using var host = await StartAsync(TestContext.Current.CancellationToken);
        using var first = await Create(host, "alpha-one");
        using var second = await Create(host, "alpha-two");

        using var duplicate = await Create(host, "alpha-one");
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var secondId = (await JsonAsync(second)).GetProperty("id").GetString();
        using var renameOnto = await host.SendAsync(HttpMethod.Patch, $"/api/v1/saved-queries/{secondId}", "writer",
            """{"name":"alpha-one"}""", IfMatch("*"));
        Assert.Equal(HttpStatusCode.Conflict, renameOnto.StatusCode);

        // Keeping its own name is not a conflict, and another project may reuse the name.
        using var keep = await host.SendAsync(HttpMethod.Patch, $"/api/v1/saved-queries/{secondId}", "writer",
            """{"name":"alpha-two","description":"same name"}""", IfMatch("*"));
        Assert.Equal(HttpStatusCode.OK, keep.StatusCode);
        using var otherProject = await host.SendAsync(HttpMethod.Post, "/api/v1/saved-queries", "beta-all",
            $$"""{"name":"alpha-one","filter":{{ErrorsFilter}}}""");
        Assert.Equal(HttpStatusCode.Created, otherProject.StatusCode);
    }

    [Fact]
    public async Task List_orders_by_name_and_pages_with_an_opaque_cursor()
    {
        await using var host = await StartAsync(TestContext.Current.CancellationToken);
        foreach (var name in new[] { "c", "a", "b" })
        {
            using var created = await Create(host, name);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        var names = new List<string>();
        string? cursor = null;
        do
        {
            using var page = await host.SendAsync(HttpMethod.Get, "/api/v1/saved-queries?limit=2" +
                (cursor is null ? "" : "&cursor=" + Uri.EscapeDataString(cursor)), "reader");
            var body = await JsonAsync(page);
            names.AddRange(body.GetProperty("items").EnumerateArray().Select(static item => item.GetProperty("name").GetString()!));
            cursor = body.TryGetProperty("next_cursor", out var next) ? next.GetString() : null;
        } while (cursor is not null);

        Assert.Equal(["a", "b", "c"], names);
    }

    [Fact]
    public async Task Idempotent_create_replays_and_conflicts_on_another_body()
    {
        await using var host = await StartAsync(TestContext.Current.CancellationToken);
        var key = new Dictionary<string, string> { ["Idempotency-Key"] = "sq-1" };

        using var first = await host.SendAsync(HttpMethod.Post, "/api/v1/saved-queries", "writer",
            $$"""{"name":"q","filter":{{ErrorsFilter}}}""", key);
        // A replay is not a duplicate name.
        using var replay = await host.SendAsync(HttpMethod.Post, "/api/v1/saved-queries", "writer",
            $$"""{"name":"q","filter":{{ErrorsFilter}}}""", key);
        using var conflict = await host.SendAsync(HttpMethod.Post, "/api/v1/saved-queries", "writer",
            $$"""{"name":"other","filter":{{ErrorsFilter}}}""", key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal((await JsonAsync(first)).GetProperty("id").GetString(), (await JsonAsync(replay)).GetProperty("id").GetString());
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Theory]
    [InlineData("""{"name":"","filter":{"signal":"traces"}}""", "name")]
    [InlineData("""{"name":"x","filter":{"signal":"traces","lookback_minutes":0}}""", "filter.lookback_minutes")]
    [InlineData("""{"name":"x","filter":{"signal":"logs","severity_min":99}}""", "filter.severity_min")]
    [InlineData("""{"name":"x","filter":{"signal":"logs","trace_id":"nope"}}""", "filter.trace_id")]
    [InlineData("""{"name":"x","filter":{"signal":"metrics","metric_name":""}}""", "filter.metric_name")]
    [InlineData("""{"name":"x","filter":{"signal":"metrics","metric_name":"m","step_ms":0}}""", "filter.step_ms")]
    [InlineData("""{"name":"x","filter":{"signal":"metrics","metric_name":"m","matchers":[{"key":"","op":"eq","value":"v"}]}}""", "filter.matchers")]
    [InlineData("""{"name":"x","filter":{"signal":"alerts"}}""", "body")]
    [InlineData("""{"name":"x"}""", "body")]
    public async Task Invalid_saved_queries_are_400_with_the_offending_field(string json, string field)
    {
        await using var host = await StartAsync(TestContext.Current.CancellationToken);

        using var response = await host.SendAsync(HttpMethod.Post, "/api/v1/saved-queries", "writer", json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(field, (await JsonAsync(response)).GetProperty("errors")[0].GetProperty("field").GetString());
    }

    [Fact]
    public async Task A_trace_run_applies_the_filter_and_look_back_and_pages_with_its_own_cursor()
    {
        await using var host = await StartAsync(TestContext.Current.CancellationToken);
        var now = TimeConversions.ToUnixNanoUnsigned(DateTimeOffset.UtcNow);
        const ulong minute = 60_000_000_000UL;
        await host.Store.EnqueueAsync(new SpanBatch(
        [
            Span(TraceId(1), SpanId(1), now - 5 * minute, now - 4 * minute, status: 2),
            Span(TraceId(2), SpanId(2), now - 3 * minute, now - 2 * minute, status: 2),
            Span(TraceId(3), SpanId(3), now - 3 * minute, now - 2 * minute),
            Span(TraceId(4), SpanId(4), now - 3 * minute, now - 2 * minute, service: "billing", status: 2),
            Span(TraceId(5), SpanId(5), now - 180 * minute, now - 179 * minute, status: 2)
        ]), TestContext.Current.CancellationToken);
        using var created = await host.SendAsync(HttpMethod.Post, "/api/v1/saved-queries", "writer",
            $$"""{"name":"errors","filter":{{ErrorsFilter}}}""");
        var id = (await JsonAsync(created)).GetProperty("id").GetString()!;

        using var first = await host.SendAsync(HttpMethod.Post, $"/api/v1/saved-queries/{id}:run", "reader",
            """{"limit":1}""");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await JsonAsync(first);
        Assert.Equal("traces", firstBody.GetProperty("signal").GetString());
        var firstItem = Assert.Single(firstBody.GetProperty("items").EnumerateArray());
        Assert.Equal(TraceId(2), firstItem.GetProperty("trace_id").GetString());
        // A run lists trace summaries; get_trace reads the span tree.
        Assert.False(firstItem.TryGetProperty("spans", out _));
        Assert.True(firstBody.GetProperty("has_more").GetBoolean());

        var cursor = firstBody.GetProperty("next_cursor").GetString()!;
        using var second = await host.SendAsync(HttpMethod.Post, $"/api/v1/saved-queries/{id}:run", "reader",
            $$"""{"limit":1,"cursor":"{{cursor}}"}""");
        var secondBody = await JsonAsync(second);
        Assert.Equal(TraceId(1), Assert.Single(secondBody.GetProperty("items").EnumerateArray())
            .GetProperty("trace_id").GetString());
        Assert.False(secondBody.GetProperty("has_more").GetBoolean());

        // The cursor belongs to this saved query only.
        using var other = await host.SendAsync(HttpMethod.Post, "/api/v1/saved-queries", "writer",
            $$"""{"name":"other","filter":{{ErrorsFilter}}}""");
        var otherId = (await JsonAsync(other)).GetProperty("id").GetString();
        using var foreign = await host.SendAsync(HttpMethod.Post, $"/api/v1/saved-queries/{otherId}:run", "reader",
            $$"""{"cursor":"{{cursor}}"}""");
        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);

        // An explicit window replaces the look-back.
        using var widened = await host.SendAsync(HttpMethod.Post, $"/api/v1/saved-queries/{id}:run", "reader",
            $$"""{"start_time":"{{DateTimeOffset.UtcNow.AddHours(-4):O}}"}""");
        Assert.Equal(3, (await JsonAsync(widened)).GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task A_log_run_filters_by_severity_and_session_inside_the_window()
    {
        await using var host = await StartAsync(TestContext.Current.CancellationToken);
        var log = new LogStorageRow
        {
            ProjectId = "alpha",
            LogId = "match",
            SessionId = "sess-1",
            ServiceName = "checkout",
            TimeUnixNano = 2_000_000_000,
            SeverityNumber = 17,
            SeverityText = "ERROR",
            Body = "\"payment failed\""
        };
        await host.Store.InsertLogsAsync(
        [
            log,
            log with { LogId = "info", SeverityNumber = 9, SeverityText = "INFO" },
            log with { LogId = "other-session", SessionId = "sess-2" },
            log with { LogId = "outside", TimeUnixNano = 9_000_000_000 }
        ], TestContext.Current.CancellationToken);
        using var created = await host.SendAsync(HttpMethod.Post, "/api/v1/saved-queries", "writer",
            """{"name":"errors","filter":{"signal":"logs","severity_min":17,"session_id":"sess-1","query":"payment"}}""");
        var id = (await JsonAsync(created)).GetProperty("id").GetString()!;

        using var run = await host.SendAsync(HttpMethod.Post, $"/api/v1/saved-queries/{id}:run", "reader",
            """{"start_time":"1970-01-01T00:00:01Z","end_time":"1970-01-01T00:00:03Z"}""");

        var body = await JsonAsync(run);
        Assert.Equal("logs", body.GetProperty("signal").GetString());
        Assert.Equal("payment failed", Assert.Single(body.GetProperty("items").EnumerateArray())
            .GetProperty("body").GetString());
    }

    [Fact]
    public async Task A_metric_run_aggregates_with_structured_matchers()
    {
        await using var host = await StartAsync(TestContext.Current.CancellationToken);
        const ulong t0 = 1_700_000_000_000_000_000UL;
        var gauge = new Gauge();
        foreach (var (route, value) in new[] { ("/api/a", 2d), ("/api/b", 4d), ("/health", 100d) })
        {
            gauge.DataPoints.Add(new NumberDataPoint
            {
                TimeUnixNano = t0,
                AsDouble = value,
                Attributes = { new KeyValue { Key = "http.route", Value = new AnyValue { StringValue = route } } }
            });
        }

        var request = new ExportMetricsServiceRequest
        {
            ResourceMetrics =
            {
                new ResourceMetrics
                {
                    Resource = new Resource(),
                    ScopeMetrics = { new ScopeMetrics { Metrics = { new OtlpMetric { Name = "active", Gauge = gauge } } } }
                }
            }
        };
        var write = MetricStorageMapper.ToStorageRows(OtlpConverter.ConvertMetrics(request), "alpha");
        await host.Store.InsertMetricsAsync(write.Series, write.Points, TestContext.Current.CancellationToken);

        using var created = await host.SendAsync(HttpMethod.Post, "/api/v1/saved-queries", "writer",
            """{"name":"api","filter":{"signal":"metrics","metric_name":"active","aggregation":"sum","step_ms":60000,"matchers":[{"key":"http.route","op":"prefix","value":"/api"}]}}""");
        var id = (await JsonAsync(created)).GetProperty("id").GetString()!;

        using var run = await host.SendAsync(HttpMethod.Post, $"/api/v1/saved-queries/{id}:run", "reader",
            """{"start_time":"2023-11-14T22:13:20Z","end_time":"2023-11-14T22:14:20Z"}""");

        var body = await JsonAsync(run);
        Assert.Equal("metrics", body.GetProperty("signal").GetString());
        var result = body.GetProperty("result");
        Assert.Equal("sum", result.GetProperty("aggregation").GetString());
        var bucket = Assert.Single(Assert.Single(result.GetProperty("series").EnumerateArray())
            .GetProperty("buckets").EnumerateArray());
        Assert.Equal(6d, bucket.GetProperty("value").GetDouble());

        using var missingMetric = await host.SendAsync(HttpMethod.Post, "/api/v1/saved-queries", "writer",
            """{"name":"missing","filter":{"signal":"metrics","metric_name":"never.recorded"}}""");
        var missingId = (await JsonAsync(missingMetric)).GetProperty("id").GetString();
        using var notFound = await host.SendAsync(HttpMethod.Post, $"/api/v1/saved-queries/{missingId}:run", "reader", "{}");
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
    }

    private static Task<HttpResponseMessage> Create(CollectorApiTestHost host, string name) =>
        host.SendAsync(HttpMethod.Post, "/api/v1/saved-queries", "writer",
            $$"""{"name":"{{name}}","filter":{{ErrorsFilter}}}""");

    private static Dictionary<string, string> IfMatch(string etag) => new() { ["If-Match"] = etag };
}
