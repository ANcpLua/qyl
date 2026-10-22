using System.Net;
using Qyl.Api.Contracts.Common.Errors;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests;

[TestClass]
public sealed class LogQueryHttpTests(TestContext testContext)
{
    private const string PercentQuery = "100%";
    private const string PercentBody = "progress 100% done";
    private const string UnderscoreQuery = "user_id";
    private const string UnderscoreBody = "login user_id=7";
    private const string NonAsciiQuery = "Größe";
    private const string NonAsciiBody = "Größe überschritten";
    private const string QuotedQuery = "\"hi\"";
    private const string QuotedBody = "said \"hi\" there";
    private const string BackslashQuery = "C:\\temp";
    private const string BackslashBody = "path C:\\temp\\x";

    [TestMethod]
    public async Task Published_log_filters_exclude_nonmatching_rows_over_http()
    {
        var ct = testContext.CancellationToken;
        await using var host = await CollectorApiTestHost.StartAsync(ct);
        var match = new LogStorageRow
        {
            ProjectId = "alpha",
            LogId = "matching",
            TraceId = "11111111111111111111111111111111",
            SessionId = "session-a",
            ServiceName = "checkout",
            TimeUnixNano = 2_000_000_000,
            SeverityNumber = 17,
            SeverityText = "ERROR",
            Body = "\"target log\""
        };
        // Each distractor differs in one filter only. Ignoring any published
        // parameter leaks an extra row into the combined query below.
        await host.Store.InsertLogsAsync(
        [
            match,
            match with { LogId = "other-service", ServiceName = "qyl.at" },
            match with { LogId = "info", SeverityNumber = 9, SeverityText = "INFO" },
            match with { LogId = "other-trace", TraceId = "22222222222222222222222222222222" },
            match with { LogId = "other-session", SessionId = "session-b" },
            match with { LogId = "before-start", TimeUnixNano = 500_000_000 },
            match with { LogId = "after-end", TimeUnixNano = 4_000_000_000 },
            match with { LogId = "other-body", Body = "\"unrelated\"" },
            match with { ProjectId = "beta", LogId = "other-project" }
        ], ct);

        using var unfiltered = await host.SendAsync(HttpMethod.Get, "/api/v1/logs", "reader");
        Assert.AreEqual(HttpStatusCode.OK, unfiltered.StatusCode);
        Assert.AreEqual(8, (await host.JsonAsync(unfiltered)).GetProperty("items").GetArrayLength());

        const string Filters = "service_name=checkout&severity_min=17"
            + "&trace_id=11111111111111111111111111111111&session_id=session-a"
            + "&start_time=1970-01-01T00%3A00%3A01Z&end_time=1970-01-01T00%3A00%3A03Z"
            + "&query=target&limit=20";
        using var filtered = await host.SendAsync(HttpMethod.Get, $"/api/v1/logs?{Filters}", "reader");
        Assert.AreEqual(HttpStatusCode.OK, filtered.StatusCode);
        var item = Assert.ContainsSingle((await host.JsonAsync(filtered)).GetProperty("items").EnumerateArray());
        Assert.AreEqual("target log", item.GetProperty("body").GetString());
        Assert.AreEqual("checkout", item.GetProperty("resource").GetProperty("service_name").GetString());
        Assert.AreEqual(17, item.GetProperty("severity_number").GetInt32());

        using var absent = await host.SendAsync(HttpMethod.Get, "/api/v1/logs?service_name=missing&severity_min=17&limit=20", "reader");
        Assert.AreEqual(HttpStatusCode.OK, absent.StatusCode);
        Assert.AreEqual(0, (await host.JsonAsync(absent)).GetProperty("items").GetArrayLength());
    }

    [TestMethod]
    [DataRow(PercentQuery, PercentBody)]
    [DataRow(UnderscoreQuery, UnderscoreBody)]
    public async Task LogSearch_LikeWildcardInText_MatchesTheTextLiterally(string query, string expected)
    {
        var ct = testContext.CancellationToken;
        await using var host = await CollectorApiTestHost.StartAsync(ct);
        var scope = new OpenTelemetry.Proto.Logs.V1.ScopeLogs();
        string[] bodies = [PercentBody, "progress 1000 done", UnderscoreBody, "login userXid=7"];
        for (var index = 0; index < bodies.Length; index++)
        {
            scope.LogRecords.Add(new OpenTelemetry.Proto.Logs.V1.LogRecord
            {
                TimeUnixNano = 2_000_000_000UL + (ulong)index,
                SeverityNumber = OpenTelemetry.Proto.Logs.V1.SeverityNumber.Info,
                Body = new OpenTelemetry.Proto.Common.V1.AnyValue { StringValue = bodies[index] }
            });
        }

        using var ingest = await host.SendProtobufAsync("/v1/logs", new OpenTelemetry.Proto.Collector.Logs.V1.ExportLogsServiceRequest
        {
            ResourceLogs = { new OpenTelemetry.Proto.Logs.V1.ResourceLogs { ScopeLogs = { scope } } }
        }, "ingester");
        Assert.AreEqual(HttpStatusCode.OK, ingest.StatusCode);

        using var response = await host.SendAsync(HttpMethod.Get, "/api/v1/logs?query=" + Uri.EscapeDataString(query), "reader");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var item = Assert.ContainsSingle((await host.JsonAsync(response)).GetProperty("items").EnumerateArray());
        Assert.AreEqual(expected, item.GetProperty("body").GetString());
    }

    [TestMethod]
    [DataRow(2, 2, false)]
    [DataRow(1, 1, true)]
    public async Task Log_pages_report_has_more_only_when_a_row_lies_past_the_limit(
        int limit, int expectedItems, bool expectedHasMore)
    {
        var ct = testContext.CancellationToken;
        await using var host = await CollectorApiTestHost.StartAsync(ct);
        var row = new LogStorageRow
        {
            ProjectId = "alpha",
            LogId = "first",
            ServiceName = "checkout",
            TimeUnixNano = 2_000_000_000,
            SeverityNumber = 9,
            SeverityText = "INFO",
            Body = "\"entry\""
        };
        // Two rows: a page that exactly fills the limit has no more.
        await host.Store.InsertLogsAsync([row, row with { LogId = "second", TimeUnixNano = 3_000_000_000 }], ct);

        using var response = await host.SendAsync(HttpMethod.Get, $"/api/v1/logs?limit={limit}", "reader");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var page = await host.JsonAsync(response);
        Assert.AreEqual(expectedItems, page.GetProperty("items").GetArrayLength());
        Assert.AreEqual(expectedHasMore, page.GetProperty("has_more").GetBoolean());
    }

    [TestMethod]
    [DataRow(NonAsciiQuery, NonAsciiBody)]
    [DataRow(QuotedQuery, QuotedBody)]
    [DataRow(BackslashQuery, BackslashBody)]
    public async Task LogSearch_TextTheStoredBodyJsonEscapes_FindsTheRecord(string query, string expected)
    {
        var ct = testContext.CancellationToken;
        await using var host = await CollectorApiTestHost.StartAsync(ct);
        var scope = new OpenTelemetry.Proto.Logs.V1.ScopeLogs();
        string[] bodies = [NonAsciiBody, QuotedBody, BackslashBody];
        for (var index = 0; index < bodies.Length; index++)
        {
            scope.LogRecords.Add(new OpenTelemetry.Proto.Logs.V1.LogRecord
            {
                TimeUnixNano = 2_000_000_000UL + (ulong)index,
                SeverityNumber = OpenTelemetry.Proto.Logs.V1.SeverityNumber.Info,
                Body = new OpenTelemetry.Proto.Common.V1.AnyValue { StringValue = bodies[index] }
            });
        }

        using var ingest = await host.SendProtobufAsync("/v1/logs", new OpenTelemetry.Proto.Collector.Logs.V1.ExportLogsServiceRequest
        {
            ResourceLogs = { new OpenTelemetry.Proto.Logs.V1.ResourceLogs { ScopeLogs = { scope } } }
        }, "ingester");
        Assert.AreEqual(HttpStatusCode.OK, ingest.StatusCode);

        using var response = await host.SendAsync(HttpMethod.Get, "/api/v1/logs?query=" + Uri.EscapeDataString(query), "reader");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var item = Assert.ContainsSingle((await host.JsonAsync(response)).GetProperty("items").EnumerateArray());
        Assert.AreEqual(expected, item.GetProperty("body").GetString());
    }

    [TestMethod]
    [DataRow("/api")]
    [DataRow("/api/v1/unknown")]
    [DataRow("/api/v2/logs")]
    public async Task Unknown_api_routes_are_problem_json_not_found(string path)
    {
        var ct = testContext.CancellationToken;
        await using var host = await CollectorApiTestHost.StartAsync(ct);
        using var response = await host.SendAsync(HttpMethod.Get, path, "writer");
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.AreEqual(ProblemDetailsMediaType.Value, response.Content.Headers.ContentType?.MediaType);
        var body = await host.JsonAsync(response);
        Assert.AreEqual(404, body.GetProperty("status").GetInt32());
        Assert.AreEqual(path, body.GetProperty("resource_id").GetString());
    }
}
