using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry.Proto.Collector.Metrics.V1;
using OpenTelemetry.Proto.Metrics.V1;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Storage;
using static Qyl.Collector.Tests.CollectorApiTestHost;

namespace Qyl.Collector.Tests;

[TestClass]
public sealed class SavedQueryLookbackTests(TestContext testContext)
{
    // Sub-millisecond boundaries must survive conversion to OTLP nanoseconds.
    private static readonly DateTimeOffset s_now = new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero).AddTicks(123);
    private const ulong StartNs = 1_792_234_800_000_012_300;
    private const ulong EndNs = 1_792_238_400_000_012_300;
    private FakeTimeProvider _clock = null!;
    private CollectorApiTestHost? _host;
    private CollectorApiTestHost Host => _host ?? throw new InvalidOperationException("The test host has not started.");

    [TestInitialize]
    public async Task StartHostAsync()
    {
        _clock = new FakeTimeProvider(s_now);
        _host = await StartAsync(testContext.CancellationToken, timeProvider: _clock);
    }

    [TestCleanup]
    public async Task StopHostAsync()
    {
        if (_host is not null) await _host.DisposeAsync();
    }

    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow("traces", false)]
    [DataRow("logs", false)]
    [DataRow("traces", true)]
    [DataRow("logs", true)]
    public async Task Runs_include_the_exact_start_and_exclude_the_exact_end(string signal, bool explicitWindow)
    {
        await SeedAsync(signal, [StartNs - 1, StartNs, StartNs + 1, EndNs - 1, EndNs, EndNs + 1]);
        var id = await CreateAsync(signal);
        // Moving the host clock must not change an explicitly supplied window.
        if (explicitWindow) _clock.Advance(TimeSpan.FromHours(2));
        var body = explicitWindow
            ? $$"""{"start_time":"{{s_now.AddHours(-1):O}}","end_time":"{{s_now:O}}"}"""
            : "{}";

        var result = await RunAsync(id, body);

        Assert.AreSequenceEqual(Ids(signal, 2, 3, 4), ResultIds(result, signal).Order(StringComparer.Ordinal));
        Assert.IsFalse(result.GetProperty("has_more").GetBoolean());
    }

    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow("traces")]
    [DataRow("logs")]
    public async Task A_new_run_recomputes_lookback_but_a_cursor_keeps_its_original_window(string signal)
    {
        await SeedAsync(signal, [StartNs, StartNs + 60_000_000_000]);
        var id = await CreateAsync(signal);
        var first = await RunAsync(id, """{"limit":1}""");
        Assert.AreSequenceEqual(Ids(signal, 2), ResultIds(first, signal));
        Assert.IsTrue(first.GetProperty("has_more").GetBoolean());
        var cursor = first.GetProperty("next_cursor").GetString();

        _clock.Advance(TimeSpan.FromTicks(1));
        var advanced = await RunAsync(id, "{}");
        Assert.AreSequenceEqual(Ids(signal, 2), ResultIds(advanced, signal));

        _clock.Advance(TimeSpan.FromHours(2));
        Assert.IsEmpty(ResultIds(await RunAsync(id, "{}"), signal));
        var continued = await RunAsync(id, $$"""{"limit":1,"cursor":"{{cursor}}"}""");
        Assert.AreSequenceEqual(Ids(signal, 1), ResultIds(continued, signal));
        Assert.IsFalse(continued.GetProperty("has_more").GetBoolean());
    }

    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow("traces", "list_traces")]
    [DataRow("logs", "search_logs")]
    public async Task Analysis_snapshot_reads_preserve_the_same_sub_millisecond_window(string signal, string operation)
    {
        await SeedAsync(signal, [StartNs - 1, StartNs, StartNs + 1, EndNs - 1, EndNs, EndNs + 1]);
        using var response = await Host.SendAsync(HttpMethod.Post, "/api/v1/analysis/" + operation, ReaderApiKey,
            $$"""{"start_time":"{{s_now.AddHours(-1):O}}","end_time":"{{s_now:O}}"}""");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var result = await Host.JsonAsync(response);

        Assert.AreSequenceEqual(Ids(signal, 2, 3, 4), result.GetProperty(signal).EnumerateArray()
            .Select(item => item.GetProperty(signal == "traces" ? "trace_id" : "body").GetString()!)
            .Order(StringComparer.Ordinal));
        Assert.AreEqual(s_now.AddHours(-1), result.GetProperty("window").GetProperty("start_time").GetDateTimeOffset());
        Assert.AreEqual(s_now, result.GetProperty("window").GetProperty("end_time").GetDateTimeOffset());
    }

    /// <summary>
    /// The contract advertises a default page size for each read; a caller that omits <c>limit</c>
    /// gets that size from the Collector, whoever calls it.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow("traces", "run", 100)]
    [DataRow("logs", "run", 100)]
    [DataRow("logs", "search_logs", 50)]
    [DataRow("traces", "list_traces", 20)]
    public async Task Reads_without_a_limit_use_the_contract_default_page_size(string signal, string read, int pageSize)
    {
        await SeedAsync(signal, [.. Enumerable.Range(0, 120).Select(i => StartNs + (ulong)i * 1_000_000_000)]);
        JsonElement result;
        if (read == "run")
        {
            result = await RunAsync(await CreateAsync(signal), "{}");
        }
        else
        {
            using var response = await Host.SendAsync(HttpMethod.Post, "/api/v1/analysis/" + read, ReaderApiKey, "{}");
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            result = await Host.JsonAsync(response);
        }

        Assert.HasCount(pageSize, result.GetProperty(read == "run" ? "items" : signal).EnumerateArray());
        Assert.IsTrue(result.GetProperty("has_more").GetBoolean());
    }

    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Metric_runs_aggregate_only_points_inside_the_exact_window(bool explicitWindow)
    {
        ulong[] times = [StartNs - 1, StartNs, StartNs + 1, EndNs - 1, EndNs, EndNs + 1];
        var gauge = new Gauge();
        for (var i = 0; i < times.Length; i++)
            gauge.DataPoints.Add(new NumberDataPoint { TimeUnixNano = times[i], AsDouble = 1 << i });
        var request = new ExportMetricsServiceRequest
        {
            ResourceMetrics = { new ResourceMetrics { ScopeMetrics =
                { new ScopeMetrics { Metrics = { new Metric { Name = "boundary", Gauge = gauge } } } } } }
        };
        var writes = MetricStorageMapper.ToStorageRows(OtlpConverter.ConvertMetrics(request), AlphaProjectId);
        await Host.Store.InsertMetricsAsync(writes.Series, writes.Observations, testContext.CancellationToken);
        using var created = await Host.SendAsync(HttpMethod.Post, "/api/v1/saved-queries", "writer",
            """{"name":"boundary","filter":{"signal":"metrics","metric_name":"boundary","lookback_minutes":60,"aggregation":"sum","step_ms":3600000}}""");
        Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
        var id = (await Host.JsonAsync(created)).GetProperty("id").GetString()!;
        if (explicitWindow) _clock.Advance(TimeSpan.FromHours(2));
        var body = explicitWindow
            ? $$"""{"start_time":"{{s_now.AddHours(-1):O}}","end_time":"{{s_now:O}}"}"""
            : "{}";

        var result = (await RunAsync(id, body)).GetProperty("result");

        var bucket = Assert.ContainsSingle(Assert.ContainsSingle(result.GetProperty("series").EnumerateArray())
            .GetProperty("buckets").EnumerateArray());
        // Distinct powers of two identify exactly the included points: 2 + 4 + 8.
        Assert.AreEqual(14d, bucket.GetProperty("value").GetDouble());
        Assert.AreEqual(s_now.AddHours(-1), result.GetProperty("start_time").GetDateTimeOffset());
        Assert.AreEqual(s_now, result.GetProperty("end_time").GetDateTimeOffset());
    }

    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow(false, "start_time", "1969-12-31T23:59:59Z")]
    [DataRow(false, "end_time", "9999-12-31T23:59:59Z")]
    [DataRow(true, "start_time", "1969-12-31T23:59:59Z")]
    [DataRow(true, "end_time", "9999-12-31T23:59:59Z")]
    public async Task Windows_outside_the_OTLP_range_report_the_invalid_field(bool analysis, string field, string timestamp)
    {
        var path = analysis ? "/api/v1/analysis/list_traces" : $"/api/v1/saved-queries/{await CreateAsync("traces")}:run";
        using var response = await Host.SendAsync(HttpMethod.Post, path, ReaderApiKey,
            $$"""{"{{field}}":"{{timestamp}}"}""");

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.AreEqual(field, (await Host.JsonAsync(response)).GetProperty("errors")[0].GetProperty("field").GetString());
    }

    private async Task SeedAsync(string signal, ulong[] times)
    {
        if (signal == "traces")
            await Host.Store.EnqueueAsync(new SpanBatch(times.Select((time, i) =>
                Span(TraceId(i + 1), SpanId(i + 1), time, time + 1)).ToArray()), testContext.CancellationToken);
        else
            await Host.Store.InsertLogsAsync(times.Select((time, i) => new LogStorageRow
            {
                ProjectId = AlphaProjectId, LogId = (i + 1).ToString(CultureInfo.InvariantCulture),
                TimeUnixNano = time, SeverityNumber = 9, Body = $"\"{i + 1}\""
            }).ToArray(), testContext.CancellationToken);
    }

    private async Task<string> CreateAsync(string signal)
    {
        using var created = await Host.SendAsync(HttpMethod.Post, "/api/v1/saved-queries", "writer",
            $$$"""{"name":"boundary","filter":{"signal":"{{{signal}}}","lookback_minutes":60}}""");
        Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
        return (await Host.JsonAsync(created)).GetProperty("id").GetString()!;
    }

    private async Task<JsonElement> RunAsync(string id, string body)
    {
        using var response = await Host.SendAsync(HttpMethod.Post, $"/api/v1/saved-queries/{id}:run", ReaderApiKey, body);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        return await Host.JsonAsync(response);
    }

    private static IEnumerable<string> ResultIds(JsonElement result, string signal) =>
        result.GetProperty("items").EnumerateArray().Select(item => item.GetProperty(signal == "traces" ? "trace_id" : "body").GetString()!);

    private static IEnumerable<string> Ids(string signal, params int[] values) =>
        values.Select(value => signal == "traces" ? TraceId(value) : value.ToString(CultureInfo.InvariantCulture));
}
