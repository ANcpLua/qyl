using System.Net;
using OpenTelemetry.Proto.Collector.Metrics.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Metrics.V1;
using OpenTelemetry.Proto.Resource.V1;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Storage;
using static Qyl.Collector.Tests.CollectorApiTestHost;
using OtlpMetric = OpenTelemetry.Proto.Metrics.V1.Metric;

namespace Qyl.Collector.Tests;

/// <summary>Contract 11.6.0 saved queries, their write base and <c>:run</c>, over the real HTTP composition.</summary>
[TestClass]
public sealed class SavedQueryApiTests(TestContext testContext)
{
    private const string ErrorsFilter = """{"signal":"traces","lookback_minutes":60,"service_name":"checkout","errors_only":true}""";

    [TestMethod]
    public async Task Create_get_patch_delete_round_trip_with_etags()
    {
        await using var host = await StartAsync(testContext.CancellationToken);

        using var created = await host.SendAsync(HttpMethod.Post, "/api/v1/saved-queries", "writer",
            $$"""{"name":"checkout errors","description":"last hour","filter":{{ErrorsFilter}}}""");
        Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
        var body = await host.JsonAsync(created);
        var id = body.GetProperty("id").GetString()!;
        Assert.AreEqual($"/api/v1/saved-queries/{id}", created.Headers.Location?.OriginalString);
        Assert.AreEqual("traces", body.GetProperty("filter").GetProperty("signal").GetString());
        Assert.IsTrue(body.GetProperty("filter").GetProperty("errors_only").GetBoolean());

        using var read = await host.SendAsync(HttpMethod.Get, $"/api/v1/saved-queries/{id}", "reader");
        Assert.AreEqual(created.Headers.ETag, read.Headers.ETag);

        using var renamed = await host.SendAsync(HttpMethod.Patch, $"/api/v1/saved-queries/{id}", "writer",
            """{"name":"renamed"}""", IfMatch(created.Headers.ETag!.ToString()));
        Assert.AreEqual(HttpStatusCode.OK, renamed.StatusCode);
        var renamedBody = await host.JsonAsync(renamed);
        // Omitted members keep their value.
        Assert.AreEqual("renamed", renamedBody.GetProperty("name").GetString());
        Assert.AreEqual("last hour", renamedBody.GetProperty("description").GetString());
        Assert.AreEqual("checkout", renamedBody.GetProperty("filter").GetProperty("service_name").GetString());

        using var refiltered = await host.SendAsync(HttpMethod.Patch, $"/api/v1/saved-queries/{id}", "writer",
            """{"filter":{"signal":"logs","severity_min":17}}""", IfMatch(renamed.Headers.ETag!.ToString()));
        Assert.AreEqual(HttpStatusCode.OK, refiltered.StatusCode);
        Assert.AreEqual("logs", (await host.JsonAsync(refiltered)).GetProperty("filter").GetProperty("signal").GetString());

        using var listLogs = await host.SendAsync(HttpMethod.Get, "/api/v1/saved-queries?signal=logs", "reader");
        Assert.ContainsSingle((await host.JsonAsync(listLogs)).GetProperty("items").EnumerateArray());
        using var listTraces = await host.SendAsync(HttpMethod.Get, "/api/v1/saved-queries?signal=traces", "reader");
        Assert.IsEmpty((await host.JsonAsync(listTraces)).GetProperty("items").EnumerateArray());

        using var stale = await host.SendAsync(HttpMethod.Delete, $"/api/v1/saved-queries/{id}", "deleter",
            headers: IfMatch(created.Headers.ETag!.ToString()));
        Assert.AreEqual(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        using var deleted = await host.SendAsync(HttpMethod.Delete, $"/api/v1/saved-queries/{id}", "deleter",
            headers: IfMatch(refiltered.Headers.ETag!.ToString()));
        Assert.AreEqual(HttpStatusCode.NoContent, deleted.StatusCode);
        using var gone = await host.SendAsync(HttpMethod.Get, $"/api/v1/saved-queries/{id}", "reader");
        Assert.AreEqual(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [TestMethod]
    public async Task Names_are_unique_per_project_on_create_and_rename()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        using var first = await Create(host, "alpha-one");
        using var second = await Create(host, "alpha-two");

        using var duplicate = await Create(host, "alpha-one");
        Assert.AreEqual(HttpStatusCode.Conflict, duplicate.StatusCode);

        var secondId = (await host.JsonAsync(second)).GetProperty("id").GetString();
        using var renameOnto = await host.SendAsync(HttpMethod.Patch, $"/api/v1/saved-queries/{secondId}", "writer",
            """{"name":"alpha-one"}""", IfMatch("*"));
        Assert.AreEqual(HttpStatusCode.Conflict, renameOnto.StatusCode);

        // Keeping its own name is not a conflict, and another project may reuse the name.
        using var keep = await host.SendAsync(HttpMethod.Patch, $"/api/v1/saved-queries/{secondId}", "writer",
            """{"name":"alpha-two","description":"same name"}""", IfMatch("*"));
        Assert.AreEqual(HttpStatusCode.OK, keep.StatusCode);
        using var otherProject = await host.SendAsync(HttpMethod.Post, "/api/v1/saved-queries", "beta-all",
            $$"""{"name":"alpha-one","filter":{{ErrorsFilter}}}""");
        Assert.AreEqual(HttpStatusCode.Created, otherProject.StatusCode);
    }

    [TestMethod]
    public async Task List_orders_by_name_and_pages_with_an_opaque_cursor()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        foreach (var name in new[] { "c", "a", "b" })
        {
            using var created = await Create(host, name);
            Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
        }

        var names = new List<string>();
        string? cursor = null;
        do
        {
            using var page = await host.SendAsync(HttpMethod.Get, "/api/v1/saved-queries?limit=2" +
                (cursor is null ? "" : "&cursor=" + Uri.EscapeDataString(cursor)), "reader");
            var body = await host.JsonAsync(page);
            names.AddRange(body.GetProperty("items").EnumerateArray().Select(static item => item.GetProperty("name").GetString()!));
            cursor = body.TryGetProperty("next_cursor", out var next) ? next.GetString() : null;
        } while (cursor is not null);

        Assert.AreSequenceEqual(["a", "b", "c"], names);
    }

    [TestMethod]
    public async Task Idempotent_create_replays_and_conflicts_on_another_body()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var key = new Dictionary<string, string> { ["Idempotency-Key"] = "sq-1" };

        using var first = await host.SendAsync(HttpMethod.Post, "/api/v1/saved-queries", "writer",
            $$"""{"name":"q","filter":{{ErrorsFilter}}}""", key);
        // A replay is not a duplicate name.
        using var replay = await host.SendAsync(HttpMethod.Post, "/api/v1/saved-queries", "writer",
            $$"""{"name":"q","filter":{{ErrorsFilter}}}""", key);
        using var conflict = await host.SendAsync(HttpMethod.Post, "/api/v1/saved-queries", "writer",
            $$"""{"name":"other","filter":{{ErrorsFilter}}}""", key);

        Assert.AreEqual(HttpStatusCode.Created, first.StatusCode);
        Assert.AreEqual(HttpStatusCode.Created, replay.StatusCode);
        Assert.AreEqual((await host.JsonAsync(first)).GetProperty("id").GetString(), (await host.JsonAsync(replay)).GetProperty("id").GetString());
        Assert.AreEqual(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    /// <summary>
    /// A create repeated under its retry key answers the saved query that key created as it is now, with its current
    /// etag. Deleting the saved query takes its key with it, so the same create makes a new one, which a run and a
    /// delete find.
    /// </summary>
    [TestMethod]
    public async Task A_repeated_create_answers_the_saved_query_as_it_is_now_and_after_a_delete_creates_it_again()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var key = new Dictionary<string, string> { ["Idempotency-Key"] = "sq-repeat" };
        var body = $$"""{"name":"q","filter":{{ErrorsFilter}}}""";
        using var first = await host.SendAsync(HttpMethod.Post, "/api/v1/saved-queries", "writer", body, key);
        var id = (await host.JsonAsync(first)).GetProperty("id").GetString()!;
        using var changed = await host.SendAsync(HttpMethod.Patch, $"/api/v1/saved-queries/{id}", "writer",
            """{"description":"changed"}""", IfMatch(first.Headers.ETag!.ToString()));

        using var repeated = await host.SendAsync(HttpMethod.Post, "/api/v1/saved-queries", "writer", body, key);

        Assert.AreEqual(HttpStatusCode.Created, repeated.StatusCode);
        var current = await host.JsonAsync(repeated);
        Assert.AreEqual((id, "changed", changed.Headers.ETag!.ToString()),
            (current.GetProperty("id").GetString(), current.GetProperty("description").GetString(), current.GetProperty("etag").GetString()));
        Assert.AreEqual(changed.Headers.ETag, repeated.Headers.ETag);

        using var deleted = await host.SendAsync(HttpMethod.Delete, $"/api/v1/saved-queries/{id}", "deleter",
            headers: IfMatch(changed.Headers.ETag!.ToString()));
        Assert.AreEqual(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.IsNull(await host.Store.GetIdempotencyKeyAsync(AlphaProjectId, "saved-queries", "sq-repeat", 0, testContext.CancellationToken));

        using var recreated = await host.SendAsync(HttpMethod.Post, "/api/v1/saved-queries", "writer", body, key);

        Assert.AreEqual(HttpStatusCode.Created, recreated.StatusCode);
        var newId = (await host.JsonAsync(recreated)).GetProperty("id").GetString()!;
        Assert.AreNotEqual(id, newId);
        using var run = await host.SendAsync(HttpMethod.Post, "/api/v1/analysis/run_saved_query", "reader", $$"""{"id":"{{newId}}"}""");
        Assert.AreEqual(HttpStatusCode.OK, run.StatusCode);
        using var deletedAgain = await host.SendAsync(HttpMethod.Delete, $"/api/v1/saved-queries/{newId}", "deleter",
            headers: IfMatch(recreated.Headers.ETag!.ToString()));
        Assert.AreEqual(HttpStatusCode.NoContent, deletedAgain.StatusCode);
    }

    /// <summary>
    /// A key whose saved query is gone, as a delete before keys went with their records left it, creates the saved
    /// query again and takes the key over.
    /// </summary>
    [TestMethod]
    public async Task A_key_whose_saved_query_is_gone_creates_it_again()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        SavedQueryRow Row(string id) => new()
        {
            ProjectId = AlphaProjectId, SavedQueryId = id, Name = id, Signal = "traces", FilterJson = """{"signal":"traces"}""",
            CreatedAtUnixNano = FixtureUnixNano, UpdatedAtUnixNano = FixtureUnixNano, Etag = "\"" + id + "\""
        };
        IdempotencyKeyRow Key(string resourceId) => new()
        {
            ProjectId = AlphaProjectId, Operation = "saved-queries", IdempotencyKey = "stale", RequestHash = "body",
            ResourceId = resourceId, ResponseJson = "{}", CreatedAtUnixNano = FixtureUnixNano
        };
        await host.Store.CreateSavedQueryAsync(Row("sq_kept"), Key("sq_gone"), 0, testContext.CancellationToken);

        var outcome = await host.Store.CreateSavedQueryAsync(Row("sq_new"), Key("sq_new"), 0, testContext.CancellationToken);

        Assert.AreEqual(ResourceWriteStatus.Created, outcome.Status);
        Assert.AreEqual("sq_new", (await host.Store.GetIdempotencyKeyAsync(AlphaProjectId, "saved-queries", "stale", 0, testContext.CancellationToken))?.ResourceId);
        var repeat = await host.Store.CreateSavedQueryAsync(Row("sq_third"), Key("sq_third"), 0, testContext.CancellationToken);
        Assert.AreEqual((ResourceWriteStatus.Replayed, "sq_new"), (repeat.Status, repeat.Row?.SavedQueryId));
    }

    [TestMethod]
    [DataRow("""{"name":"","filter":{"signal":"traces"}}""", "name")]
    [DataRow("""{"name":"x","filter":{"signal":"traces","lookback_minutes":0}}""", "filter.lookback_minutes")]
    [DataRow("""{"name":"x","filter":{"signal":"logs","severity_min":99}}""", "filter.severity_min")]
    [DataRow("""{"name":"x","filter":{"signal":"logs","trace_id":"nope"}}""", "filter.trace_id")]
    [DataRow("""{"name":"x","filter":{"signal":"metrics","metric_name":""}}""", "filter.metric_name")]
    [DataRow("""{"name":"x","filter":{"signal":"metrics","metric_name":"m","step_ms":0}}""", "filter.step_ms")]
    [DataRow("""{"name":"x","filter":{"signal":"metrics","metric_name":"m","matchers":[{"key":"","op":"eq","value":"v"}]}}""", "filter.matchers")]
    [DataRow("""{"name":"x","filter":{"signal":"alerts"}}""", "body")]
    [DataRow("""{"name":"x"}""", "body")]
    public async Task Invalid_saved_queries_are_400_with_the_offending_field(string json, string field)
    {
        await using var host = await StartAsync(testContext.CancellationToken);

        using var response = await host.SendAsync(HttpMethod.Post, "/api/v1/saved-queries", "writer", json);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.AreEqual(field, (await host.JsonAsync(response)).GetProperty("errors")[0].GetProperty("field").GetString());
    }

    [TestMethod]
    public async Task A_trace_run_applies_the_filter_and_look_back_and_pages_with_its_own_cursor()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        const ulong now = FixtureUnixNano;
        const ulong Minute = 60_000_000_000UL;
        await host.Store.EnqueueAsync(new SpanBatch(
        [
            Span(TraceId(1), SpanId(1), now - 5 * Minute, now - 4 * Minute, status: 2),
            Span(TraceId(2), SpanId(2), now - 3 * Minute, now - 2 * Minute, status: 2),
            Span(TraceId(3), SpanId(3), now - 3 * Minute, now - 2 * Minute),
            Span(TraceId(4), SpanId(4), now - 3 * Minute, now - 2 * Minute, service: "billing", status: 2),
            Span(TraceId(5), SpanId(5), now - 180 * Minute, now - 179 * Minute, status: 2)
        ]), testContext.CancellationToken);
        using var created = await host.SendAsync(HttpMethod.Post, "/api/v1/saved-queries", "writer",
            $$"""{"name":"errors","filter":{{ErrorsFilter}}}""");
        var id = (await host.JsonAsync(created)).GetProperty("id").GetString()!;

        using var first = await host.SendAsync(HttpMethod.Post, $"/api/v1/saved-queries/{id}:run", "reader",
            """{"limit":1}""");
        Assert.AreEqual(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await host.JsonAsync(first);
        Assert.AreEqual("traces", firstBody.GetProperty("signal").GetString());
        var firstItem = Assert.ContainsSingle(firstBody.GetProperty("items").EnumerateArray());
        Assert.AreEqual(TraceId(2), firstItem.GetProperty("trace_id").GetString());
        // A run lists trace summaries; get_trace reads the span tree.
        Assert.IsFalse(firstItem.TryGetProperty("spans", out _));
        Assert.IsTrue(firstBody.GetProperty("has_more").GetBoolean());

        var cursor = firstBody.GetProperty("next_cursor").GetString()!;
        using var second = await host.SendAsync(HttpMethod.Post, $"/api/v1/saved-queries/{id}:run", "reader",
            $$"""{"limit":1,"cursor":"{{cursor}}"}""");
        var secondBody = await host.JsonAsync(second);
        Assert.AreEqual(TraceId(1), Assert.ContainsSingle(secondBody.GetProperty("items").EnumerateArray())
            .GetProperty("trace_id").GetString());
        Assert.IsFalse(secondBody.GetProperty("has_more").GetBoolean());

        // The cursor belongs to this saved query only.
        using var other = await host.SendAsync(HttpMethod.Post, "/api/v1/saved-queries", "writer",
            $$"""{"name":"other","filter":{{ErrorsFilter}}}""");
        var otherId = (await host.JsonAsync(other)).GetProperty("id").GetString();
        using var foreign = await host.SendAsync(HttpMethod.Post, $"/api/v1/saved-queries/{otherId}:run", "reader",
            $$"""{"cursor":"{{cursor}}"}""");
        Assert.AreEqual(HttpStatusCode.BadRequest, foreign.StatusCode);

        // An explicit window replaces the look-back.
        using var widened = await host.SendAsync(HttpMethod.Post, $"/api/v1/saved-queries/{id}:run", "reader",
            $$"""{"start_time":"{{FixtureUtcNow.AddHours(-4):O}}"}""");
        Assert.AreEqual(3, (await host.JsonAsync(widened)).GetProperty("items").GetArrayLength());
    }

    /// <summary>A run's limit keeps the contract's range, as the analysis route's does: 1001 is refused naming it, 1000 runs.</summary>
    [TestMethod]
    public async Task A_run_refuses_a_limit_outside_the_contracts_range()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        using var created = await Create(host, "errors");
        var id = (await host.JsonAsync(created)).GetProperty("id").GetString()!;

        using var refused = await host.SendAsync(HttpMethod.Post, $"/api/v1/saved-queries/{id}:run", "reader", """{"limit":1001}""");
        using var accepted = await host.SendAsync(HttpMethod.Post, $"/api/v1/saved-queries/{id}:run", "reader", """{"limit":1000}""");

        Assert.AreEqual(HttpStatusCode.BadRequest, refused.StatusCode);
        var error = (await host.JsonAsync(refused)).GetProperty("errors")[0];
        Assert.AreEqual(("limit", "Use limit from 1 to 1000.", "Omit limit for 100."),
            (error.GetProperty("field").GetString(), error.GetProperty("message").GetString(), error.GetProperty("hint").GetString()));
        Assert.AreEqual(HttpStatusCode.OK, accepted.StatusCode);
    }

    /// <summary>
    /// A run takes the window and page its contract declares and nothing else: lookback_minutes, which only the analysis
    /// route's run_saved_query takes, is refused naming the members a run accepts, a billion minutes as well, and a limit
    /// sent as a string is refused, not read as 100.
    /// </summary>
    [TestMethod]
    [DataRow("""{"lookback_minutes":1000000000}""", "lookback_minutes", "is not accepted here; accepted: start_time, end_time, limit, cursor")]
    [DataRow("""{"lookback_minutes":60}""", "lookback_minutes", "is not accepted here; accepted: start_time, end_time, limit, cursor")]
    [DataRow("""{"limit":"5"}""", "limit", "must be integer")]
    [DataRow("""{"start_time":5}""", "start_time", "must be string")]
    public async Task A_run_refuses_a_member_its_contract_does_not_declare_or_types_otherwise(string body, string field, string message)
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        using var created = await Create(host, "errors");
        var id = (await host.JsonAsync(created)).GetProperty("id").GetString()!;

        using var refused = await host.SendAsync(HttpMethod.Post, $"/api/v1/saved-queries/{id}:run", "reader", body);

        Assert.AreEqual(HttpStatusCode.BadRequest, refused.StatusCode);
        var error = (await host.JsonAsync(refused)).GetProperty("errors")[0];
        Assert.AreEqual((field, message), (error.GetProperty("field").GetString(), error.GetProperty("message").GetString()));
    }

    [TestMethod]
    public async Task A_log_run_filters_by_severity_and_session_inside_the_window()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
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
        ], testContext.CancellationToken);
        using var created = await host.SendAsync(HttpMethod.Post, "/api/v1/saved-queries", "writer",
            """{"name":"errors","filter":{"signal":"logs","severity_min":17,"session_id":"sess-1","query":"payment"}}""");
        var id = (await host.JsonAsync(created)).GetProperty("id").GetString()!;

        using var run = await host.SendAsync(HttpMethod.Post, $"/api/v1/saved-queries/{id}:run", "reader",
            """{"start_time":"1970-01-01T00:00:01Z","end_time":"1970-01-01T00:00:03Z"}""");

        var body = await host.JsonAsync(run);
        Assert.AreEqual("logs", body.GetProperty("signal").GetString());
        Assert.AreEqual("payment failed", Assert.ContainsSingle(body.GetProperty("items").EnumerateArray())
            .GetProperty("body").GetString());
    }

    [TestMethod]
    public async Task A_metric_run_aggregates_with_structured_matchers()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        const ulong T0 = 1_700_000_000_000_000_000UL;
        var gauge = new Gauge();
        foreach (var (route, value) in new[] { ("/api/a", 2d), ("/api/b", 4d), ("/health", 100d) })
        {
            gauge.DataPoints.Add(new NumberDataPoint
            {
                TimeUnixNano = T0,
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
        await host.Store.InsertMetricsAsync(write.Series, write.Observations, testContext.CancellationToken);

        using var created = await host.SendAsync(HttpMethod.Post, "/api/v1/saved-queries", "writer",
            """{"name":"api","filter":{"signal":"metrics","metric_name":"active","aggregation":"sum","step_ms":60000,"matchers":[{"key":"http.route","op":"prefix","value":"/api"}]}}""");
        var id = (await host.JsonAsync(created)).GetProperty("id").GetString()!;

        using var run = await host.SendAsync(HttpMethod.Post, $"/api/v1/saved-queries/{id}:run", "reader",
            """{"start_time":"2023-11-14T22:13:20Z","end_time":"2023-11-14T22:14:20Z"}""");

        var body = await host.JsonAsync(run);
        Assert.AreEqual("metrics", body.GetProperty("signal").GetString());
        var result = body.GetProperty("result");
        Assert.AreEqual("sum", result.GetProperty("aggregation").GetString());
        var bucket = Assert.ContainsSingle(Assert.ContainsSingle(result.GetProperty("series").EnumerateArray())
            .GetProperty("buckets").EnumerateArray());
        Assert.AreEqual(6d, bucket.GetProperty("value").GetDouble());

        using var missingMetric = await host.SendAsync(HttpMethod.Post, "/api/v1/saved-queries", "writer",
            """{"name":"missing","filter":{"signal":"metrics","metric_name":"never.recorded"}}""");
        var missingId = (await host.JsonAsync(missingMetric)).GetProperty("id").GetString();
        using var notFound = await host.SendAsync(HttpMethod.Post, $"/api/v1/saved-queries/{missingId}:run", "reader", "{}");
        Assert.AreEqual(HttpStatusCode.NotFound, notFound.StatusCode);
        // Like the analysis route, the run's 404 names what is missing and the tool that lists what exists.
        var missing = await host.JsonAsync(notFound);
        Assert.AreEqual(("metric", "never.recorded"), (missing.GetProperty("resource_type").GetString(), missing.GetProperty("resource_id").GetString()));
        StringAssert.Contains(missing.GetProperty("detail").GetString(), "list_metrics");
    }

    private static Task<HttpResponseMessage> Create(CollectorApiTestHost host, string name) =>
        host.SendAsync(HttpMethod.Post, "/api/v1/saved-queries", "writer",
            $$"""{"name":"{{name}}","filter":{{ErrorsFilter}}}""");

    private static Dictionary<string, string> IfMatch(string etag) => new() { ["If-Match"] = etag };
}
