using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Qyl.Collector.Hosting;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests;

public sealed class LogQueryHttpTests
{
    [Fact]
    public async Task Published_log_filters_exclude_nonmatching_rows_over_http()
    {
        var ct = TestContext.Current.CancellationToken;
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
        using var client = new HttpClient { BaseAddress = new Uri(Assert.Single(app.Urls)) };
        try
        {
            using var unfiltered = JsonDocument.Parse(await client.GetStringAsync("/api/v1/logs", ct));
            Assert.Equal(8, unfiltered.RootElement.GetProperty("items").GetArrayLength());

            const string filters = "service_name=checkout&severity_min=17"
                + "&trace_id=11111111111111111111111111111111&session_id=session-a"
                + "&start_time=1970-01-01T00%3A00%3A01Z&end_time=1970-01-01T00%3A00%3A03Z"
                + "&query=target&limit=20";
            using var filtered = JsonDocument.Parse(await client.GetStringAsync($"/api/v1/logs?{filters}", ct));
            var item = Assert.Single(filtered.RootElement.GetProperty("items").EnumerateArray());
            Assert.Equal("target log", item.GetProperty("body").GetString());
            Assert.Equal("checkout", item.GetProperty("resource").GetProperty("service_name").GetString());
            Assert.Equal(17, item.GetProperty("severity_number").GetInt32());

            using var absent = JsonDocument.Parse(await client.GetStringAsync(
                "/api/v1/logs?service_name=missing&severity_min=17&limit=20", ct));
            Assert.Equal(0, absent.RootElement.GetProperty("items").GetArrayLength());
        }
        finally
        {
            await app.StopAsync(ct);
        }
    }
}
