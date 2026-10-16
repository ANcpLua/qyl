using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Qyl.Api.Contracts.Common.Errors;
using Qyl.Collector.Hosting;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests;

[TestClass]
public sealed class LogQueryHttpTests(TestContext testContext)
{
    private static readonly HttpClient s_client = new();

    [TestMethod]
    public async Task Published_log_filters_exclude_nonmatching_rows_over_http()
    {
        var ct = testContext.CancellationToken;
        await using var store = new DuckDbStore(":memory:");
        var match = new LogStorageRow
        {
            ProjectId = "default",
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
        await store.InsertLogsAsync(
        [
            match,
            match with { LogId = "other-service", ServiceName = "qyl.at" },
            match with { LogId = "info", SeverityNumber = 9, SeverityText = "INFO" },
            match with { LogId = "other-trace", TraceId = "22222222222222222222222222222222" },
            match with { LogId = "other-session", SessionId = "session-b" },
            match with { LogId = "before-start", TimeUnixNano = 500_000_000 },
            match with { LogId = "after-end", TimeUnixNano = 4_000_000_000 },
            match with { LogId = "other-body", Body = "\"unrelated\"" },
            match with { ProjectId = "other-project", LogId = "other-project" }
        ], ct);

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton<IQylStore>(store);
        builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, QylSerializerContext.Default));
        await using var app = builder.Build();
        app.MapGet("/api/v1/logs", CollectorEndpointExtensions.GetLogsAsync);
        await app.StartAsync(ct);
        var baseAddress = new Uri(Assert.ContainsSingle(app.Urls));
        try
        {
            using var unfiltered = JsonDocument.Parse(await s_client.GetStringAsync(new Uri(baseAddress, "/api/v1/logs"), ct));
            Assert.AreEqual(8, unfiltered.RootElement.GetProperty("items").GetArrayLength());

            const string Filters = "service_name=checkout&severity_min=17"
                + "&trace_id=11111111111111111111111111111111&session_id=session-a"
                + "&start_time=1970-01-01T00%3A00%3A01Z&end_time=1970-01-01T00%3A00%3A03Z"
                + "&query=target&limit=20";
            using var filtered = JsonDocument.Parse(await s_client.GetStringAsync(new Uri(baseAddress, $"/api/v1/logs?{Filters}"), ct));
            var item = Assert.ContainsSingle(filtered.RootElement.GetProperty("items").EnumerateArray());
            Assert.AreEqual("target log", item.GetProperty("body").GetString());
            Assert.AreEqual("checkout", item.GetProperty("resource").GetProperty("service_name").GetString());
            Assert.AreEqual(17, item.GetProperty("severity_number").GetInt32());

            using var absent = JsonDocument.Parse(await s_client.GetStringAsync(new Uri(baseAddress, "/api/v1/logs?service_name=missing&severity_min=17&limit=20"), ct));
            Assert.AreEqual(0, absent.RootElement.GetProperty("items").GetArrayLength());
        }
        finally
        {
            await app.StopAsync(ct);
        }
    }

    [TestMethod]
    [DataRow(2, 2, false)]
    [DataRow(1, 1, true)]
    public async Task Log_pages_report_has_more_only_when_a_row_lies_past_the_limit(
        int limit, int expectedItems, bool expectedHasMore)
    {
        var ct = testContext.CancellationToken;
        await using var store = new DuckDbStore(":memory:");
        var row = new LogStorageRow
        {
            ProjectId = "default",
            LogId = "first",
            ServiceName = "checkout",
            TimeUnixNano = 2_000_000_000,
            SeverityNumber = 9,
            SeverityText = "INFO",
            Body = "\"entry\""
        };
        // Two rows: a page that exactly fills the limit has no more.
        await store.InsertLogsAsync([row, row with { LogId = "second", TimeUnixNano = 3_000_000_000 }], ct);

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton<IQylStore>(store);
        builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, QylSerializerContext.Default));
        await using var app = builder.Build();
        app.MapGet("/api/v1/logs", CollectorEndpointExtensions.GetLogsAsync);
        await app.StartAsync(ct);
        var baseAddress = new Uri(Assert.ContainsSingle(app.Urls));
        try
        {
            using var page = JsonDocument.Parse(await s_client.GetStringAsync(new Uri(baseAddress, $"/api/v1/logs?limit={limit}"), ct));
            Assert.AreEqual(expectedItems, page.RootElement.GetProperty("items").GetArrayLength());
            Assert.AreEqual(expectedHasMore, page.RootElement.GetProperty("has_more").GetBoolean());
        }
        finally
        {
            await app.StopAsync(ct);
        }
    }

    [TestMethod]
    [DataRow("/api")]
    [DataRow("/api/v1/unknown")]
    [DataRow("/api/v2/logs")]
    public async Task Unknown_api_routes_are_problem_json_not_found(string path)
    {
        var ct = testContext.CancellationToken;
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build();
        app.MapFallback(CollectorEndpointExtensions.FallbackHandler);
        await app.StartAsync(ct);
        var baseAddress = new Uri(Assert.ContainsSingle(app.Urls));
        try
        {
            using var response = await s_client.GetAsync(new Uri(baseAddress, path), ct);
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
            Assert.AreEqual(ProblemDetailsMediaType.Value, response.Content.Headers.ContentType?.MediaType);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            Assert.AreEqual(404, body.RootElement.GetProperty("status").GetInt32());
            Assert.AreEqual(path, body.RootElement.GetProperty("resource_id").GetString());

            // Outside /api the fallback keeps serving the dashboard path, never problem+json.
            using var dashboard = await s_client.GetAsync(new Uri(baseAddress, "/sessions"), ct);
            Assert.AreNotEqual(ProblemDetailsMediaType.Value, dashboard.Content.Headers.ContentType?.MediaType);
        }
        finally
        {
            await app.StopAsync(ct);
        }
    }
}
