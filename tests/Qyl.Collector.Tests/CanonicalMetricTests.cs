using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using DuckDB.NET.Data;
using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Metrics.V1;
using OpenTelemetry.Proto.Metrics.V1;
using Qyl.Api.Contracts.Common;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Mapping;
using Qyl.Collector.Storage;
using Contract = Qyl.Api.Contracts.OTel.Metrics;
using Kind = Qyl.Collector.Ingestion.Primitives.MetricKind;
using Temporality = Qyl.Collector.Ingestion.Primitives.MetricTemporality;

namespace Qyl.Collector.Tests;

[TestClass]
public sealed class CanonicalMetricTests(TestContext testContext)
{
    private const ulong Time = 1_700_000_000_000_000_000;
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task Exact_wire_int64_and_ordered_full_exemplars_survive_reopen()
    {
        var wire = new NumberDataPoint { TimeUnixNano = Time, AsInt = 9_007_199_254_740_993, Flags = uint.MaxValue - 1 };
        for (var i = 0; i < 5; i++)
            wire.Exemplars.Add(new Exemplar
            {
                TimeUnixNano = Time + (ulong)i, AsInt = long.MaxValue - i,
                TraceId = ByteString.CopyFrom([.. Enumerable.Repeat((byte)0x12, 16)]),
                SpanId = ByteString.CopyFrom([.. Enumerable.Repeat((byte)(0x34 + i), 8)]),
                FilteredAttributes = { new OpenTelemetry.Proto.Common.V1.KeyValue
                { Key = "http.response.status_code", Value = new OpenTelemetry.Proto.Common.V1.AnyValue { IntValue = i } } }
            });
        var request = Request(new Metric { Name = "exact.integer", Gauge = new Gauge { DataPoints = { wire } } });
        var converted = OtlpConverter.ConvertMetrics(request);
        var point = Assert.ContainsSingle(converted.Points);
        var path = Path.Combine(Path.GetTempPath(), $"qyl-canonical-{Guid.NewGuid():N}.duckdb");
        try
        {
            await using (var store = new DuckDbStore(path)) await Insert(store, [point]);
            await using var reopened = new DuckDbStore(path);
            var series = Assert.ContainsSingle(await reopened.ListMetricSeriesAsync("alpha", "exact.integer", [], ct: Ct));
            var stored = Assert.ContainsSingle(await Read(reopened, series));
            var contract = MetricMapper.ToContract(stored, series);
            Assert.AreEqual(9_007_199_254_740_993, Assert.IsInstanceOfType<AttributeIntValue>(Assert.IsInstanceOfType<Contract.MetricNumberPointData>(contract.Data).Value).Value);
            Assert.AreEqual(uint.MaxValue - 1, contract.Flags);
            Assert.IsNotNull(contract.Exemplars);
            Assert.HasCount(5, contract.Exemplars);
            for (var i = 0; i < 5; i++)
            {
                var exemplar = contract.Exemplars[i];
                Assert.AreEqual(Time + (ulong)i, exemplar.TimeUnixNano);
                Assert.AreEqual(long.MaxValue - i, Assert.IsInstanceOfType<AttributeIntValue>(exemplar.Value).Value);
                Assert.AreEqual(string.Concat(Enumerable.Repeat("12", 16)), exemplar.TraceId);
                Assert.AreEqual(Convert.ToHexStringLower([.. Enumerable.Repeat((byte)(0x34 + i), 8)]), exemplar.SpanId);
                Assert.AreEqual(i, Assert.IsInstanceOfType<AttributeIntValue>(Assert.IsInstanceOfType<AttributeValue.ObjectValue>(Assert.ContainsSingle(exemplar.FilteredAttributes).Value).Value).Value);
            }
            var json = JsonSerializer.Serialize(contract, global::Qyl.Api.Contracts.ContractTypes.TypeInfo<global::Qyl.Api.Contracts.OTel.Metrics.MetricPoint>());
            using var parsed = JsonDocument.Parse(json);
            Assert.AreEqual("9007199254740993", parsed.RootElement.GetProperty("data").GetProperty("value").GetProperty("value").GetString());
        }
        finally { Delete(path); }
    }

    [TestMethod]
    [DataRow(double.NaN, "NaN")]
    [DataRow(double.PositiveInfinity, "Infinity")]
    [DataRow(double.NegativeInfinity, "-Infinity")]
    public async Task Special_float64_values_remain_original_named_values(double value, string wireValue)
    {
        await using var store = new DuckDbStore(":memory:");
        var input = Number(0) with { Data = new Contract.MetricNumberPointData { Value = new AttributeDoubleValue { Value = value } } };
        await Insert(store, [input]);
        var series = Assert.ContainsSingle(await store.ListMetricSeriesAsync("alpha", input.MetricName, [], ct: Ct));
        var raw = MetricMapper.ToContract(Assert.ContainsSingle(await Read(store, series)), series);
        var actual = Assert.IsInstanceOfType<AttributeDoubleValue>(Assert.IsInstanceOfType<Contract.MetricNumberPointData>(raw.Data).Value).Value;
        if (double.IsNaN(value)) Assert.IsTrue(double.IsNaN(actual)); else Assert.AreEqual(value, actual);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(raw, global::Qyl.Api.Contracts.ContractTypes.TypeInfo<global::Qyl.Api.Contracts.OTel.Metrics.MetricPoint>()));
        Assert.AreEqual(wireValue, json.RootElement.GetProperty("data").GetProperty("value").GetProperty("value").GetString());
    }

    [TestMethod]
    public async Task Contradictions_are_visible_but_retry_and_exemplar_enrichment_do_not_double_count()
    {
        await using var store = new DuckDbStore(":memory:");
        var initial = Number(42);
        var enriched = initial with { ResourceDroppedAttributesCount = 13, ScopeDroppedAttributesCount = 17, Exemplars = [new Contract.MetricExemplar { TimeUnixNano = Time,
            Value = new AttributeIntValue { Value = 42 }, FilteredAttributes = [] }] };
        var equivalent = initial with { Data = new Contract.MetricNumberPointData { Value = new AttributeDoubleValue { Value = 42 } } };
        await Insert(store, [initial, initial, enriched, equivalent]);
        var series = Assert.ContainsSingle(await store.ListMetricSeriesAsync("alpha", initial.MetricName, [], ct: Ct));
        var observations = await Read(store, series);
        Assert.HasCount(3, observations);
        Assert.IsTrue(observations.All(static row => !row.HasConflict));
        var enrichedRaw = MetricMapper.ToContract(Assert.ContainsSingle(row => row.Observation.ResourceDroppedAttributesCount == 13, observations), series);
        Assert.AreEqual(13U, enrichedRaw.ResourceDroppedAttributesCount);
        Assert.AreEqual(17U, enrichedRaw.ScopeDroppedAttributesCount);
        var query = Range(series);
        var aggregated = Assert.ContainsSingle(await store.QueryMetricAsync(query, Ct));
        Assert.AreEqual(42d, Assert.ContainsSingle(aggregated.Points).Value);
        await Insert(store, [Number(43)]);
        observations = await Read(store, series);
        Assert.HasCount(4, observations);
        Assert.IsTrue(observations.All(static row => row.HasConflict));
        await Assert.ThrowsExactlyAsync<MetricQueryConflictException>(() => store.QueryMetricAsync(query, Ct));
    }

    [TestMethod]
    [DataRow(20, 4099)]
    [DataRow(-1024, 1)]
    public async Task Native_exponential_histogram_remains_available_when_projection_is_unrepresentable(int scale, int bucketCount)
    {
        await using var store = new DuckDbStore(":memory:");
        var data = new Contract.MetricExponentialHistogramPointData
        {
            Count = (ulong)bucketCount + 1, Sum = 1, Min = 0, Max = 2, Scale = scale, ZeroCount = 1, ZeroThreshold = 0.001,
            Positive = new Contract.MetricExponentialBuckets { Offset = 1, BucketCounts = [.. Enumerable.Repeat(1UL, bucketCount)] },
            Negative = new Contract.MetricExponentialBuckets { Offset = -88, BucketCounts = [] }
        };
        var input = Number(1) with { Kind = Kind.ExponentialHistogram, Data = data };
        await Insert(store, [input]);
        var series = Assert.ContainsSingle(await store.ListMetricSeriesAsync("alpha", input.MetricName, [], ct: Ct));
        var raw = MetricMapper.ToContract(Assert.ContainsSingle(await Read(store, series)), series);
        var actual = Assert.IsInstanceOfType<Contract.MetricExponentialHistogramPointData>(raw.Data);
        Assert.AreEqual(scale, actual.Scale);
        Assert.IsNotNull(actual.Positive);
        Assert.AreEqual(1, actual.Positive.Offset);
        Assert.HasCount(bucketCount, actual.Positive.BucketCounts);
        Assert.AreEqual(1UL, actual.ZeroCount);
        Assert.IsNotNull(raw.ProjectionUnavailableReason);
        await Assert.ThrowsExactlyAsync<MetricProjectionUnavailableException>(() => store.QueryMetricAsync(Range(series) with { Aggregation = MetricAggregation.P95 }, Ct));
    }

    [TestMethod]
    public async Task Summary_is_retained_and_unsupported_aggregate_is_explicit()
    {
        await using var store = new DuckDbStore(":memory:");
        var request = Request(new Metric
        {
            Name = "wire.summary", Summary = new Summary
            {
                DataPoints = { new SummaryDataPoint
                {
                    TimeUnixNano = Time, Count = 7, Sum = 20,
                    QuantileValues = { new SummaryDataPoint.Types.ValueAtQuantile { Quantile = 0.95, Value = 6 } }
                } }
            }
        });
        var converted = OtlpConverter.ConvertMetrics(request);
        Assert.AreEqual(0L, converted.RejectedDataPoints);
        var point = Assert.ContainsSingle(converted.Points);
        await Insert(store, [point]);
        var series = Assert.ContainsSingle(await store.ListMetricSeriesAsync("alpha", point.MetricName, [], ct: Ct));
        var raw = MetricMapper.ToContract(Assert.ContainsSingle(await Read(store, series)), series);
        Assert.AreEqual(6d, Assert.ContainsSingle(Assert.IsInstanceOfType<Contract.MetricSummaryPointData>(raw.Data).QuantileValues).Value);
        await Assert.ThrowsExactlyAsync<MetricProjectionUnavailableException>(() => store.QueryMetricAsync(Range(series), Ct));
    }

    [TestMethod]
    public async Task Http_and_a_raw_query_metric_page_all_observations_with_exact_values()
    {
        await using var host = await CollectorApiTestHost.StartAsync(Ct);
        await Insert(host.Store, [.. Enumerable.Range(0, 301).Select(i => Number(9_007_199_254_740_993 + i) with { TimeUnixNano = Time + (ulong)i })]);
        var series = Assert.ContainsSingle(await host.Store.ListMetricSeriesAsync("alpha", "canonical.number", [], ct: Ct));
        var ids = new HashSet<string>(StringComparer.Ordinal); string? cursor = null;
        do
        {
            using var response = await host.SendAsync(HttpMethod.Get,
                $"/api/v1/metrics/canonical.number/series/{series.SeriesId}/points?limit=73" + (cursor is null ? "" : "&cursor=" + Uri.EscapeDataString(cursor)), "reader");
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            var page = await host.JsonAsync(response);
            foreach (var item in page.GetProperty("items").EnumerateArray()) Assert.IsTrue(ids.Add(item.GetProperty("observation_id").GetString()!));
            cursor = page.TryGetProperty("next_cursor", out var next) ? next.GetString() : null;
        } while (cursor is not null);
        Assert.HasCount(301, ids);
        using var selected = await host.SendAsync(HttpMethod.Post, "/api/v1/analysis/query_metric", "reader",
            new JsonObject { ["metric_name"] = "canonical.number", ["series_id"] = series.SeriesId, ["raw"] = true, ["limit"] = 11,
                ["start_time"] = "2023-11-14T22:13:19Z", ["end_time"] = "2023-11-14T22:13:21Z" }.ToJsonString());
        var result = await host.JsonAsync(selected);
        Assert.AreEqual(HttpStatusCode.OK, selected.StatusCode, result.GetRawText());
        Assert.AreEqual((series.SeriesId, "gauge"), (result.GetProperty("series_id").GetString(), result.GetProperty("kind").GetString()));
        Assert.AreEqual(11, result.GetProperty("points").GetArrayLength());
        Assert.IsTrue(result.GetProperty("has_more").GetBoolean());
        Assert.IsFalse(result.TryGetProperty("series", out _), "a raw read aggregates nothing");
        // Exact int64 values in their lossless form at RFC 3339 times, newest first.
        static string Value(JsonElement point) => point.GetProperty("data").GetProperty("value").GetProperty("value").GetString()!;
        var values = result.GetProperty("points").EnumerateArray().Select(Value).ToHashSet(StringComparer.Ordinal);
        Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000).AddTicks(3), result.GetProperty("points")[0].GetProperty("time").GetDateTimeOffset());
        while (result.GetProperty("has_more").GetBoolean())
        {
            using var continuation = await host.SendAsync(HttpMethod.Post, "/api/v1/analysis/query_metric", "reader",
                new JsonObject { ["metric_name"] = "canonical.number", ["raw"] = true, ["limit"] = 73,
                    ["cursor"] = result.GetProperty("next_cursor").GetString() }.ToJsonString());
            Assert.AreEqual(HttpStatusCode.OK, continuation.StatusCode);
            result = await host.JsonAsync(continuation);
            Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(1_699_999_999), result.GetProperty("window").GetProperty("start_time").GetDateTimeOffset());
            foreach (var point in result.GetProperty("points").EnumerateArray()) Assert.IsTrue(values.Add(Value(point)));
        }
        Assert.HasCount(301, values);
        Assert.Contains("9007199254741293", values);

        using var badCursor = await host.SendAsync(HttpMethod.Get,
            $"/api/v1/metrics/canonical.number/series/{series.SeriesId}/points?cursor=invalid", "reader");
        Assert.AreEqual(HttpStatusCode.BadRequest, badCursor.StatusCode);
        await Insert(host.Store, [Number(999)]);
        using var conflicting = await host.SendAsync(HttpMethod.Get,
            $"/api/v1/metrics/canonical.number/query?series_id={series.SeriesId}&start_time=2023-11-14T22:13:19Z&end_time=2023-11-14T22:13:21Z", "reader");
        Assert.AreEqual(HttpStatusCode.Conflict, conflicting.StatusCode);
    }

    /// <summary>
    /// list_metrics counts each attribute key's values per instrument and suggests the keys to group by; a raw read
    /// takes one series and, when the arguments select several, names their series ids.
    /// </summary>
    [TestMethod]
    public async Task List_metrics_counts_key_values_and_a_raw_read_names_the_series_it_cannot_choose_between()
    {
        await using var host = await CollectorApiTestHost.StartAsync(Ct);
        await Insert(host.Store,
        [
            Number(1) with { Attributes = new Dictionary<string, OtlpAttributeValue> { ["http.request.method"] = OtlpAttributeValue.FromString("GET") } },
            Number(2) with { Attributes = new Dictionary<string, OtlpAttributeValue> { ["http.request.method"] = OtlpAttributeValue.FromString("POST") } }
        ]);
        var (status, listed) = await AskAsync(host, "list_metrics", []);
        Assert.AreEqual(HttpStatusCode.OK, status, listed.GetRawText());
        var instrument = Assert.ContainsSingle(listed.GetProperty("items").EnumerateArray());
        var key = Assert.ContainsSingle(instrument.GetProperty("keys").EnumerateArray());
        Assert.AreEqual(("http.request.method", 2), (key.GetProperty("key").GetString(), key.GetProperty("cardinality").GetInt32()));
        Assert.AreSequenceEqual(["http.request.method"], instrument.GetProperty("suggested_group_by").EnumerateArray().Select(static k => k.GetString()));

        var (ambiguousStatus, ambiguous) = await AskAsync(host, "query_metric", new JsonObject { ["metric_name"] = "canonical.number", ["raw"] = true });
        Assert.AreEqual(HttpStatusCode.BadRequest, ambiguousStatus, ambiguous.GetRawText());
        var error = Assert.ContainsSingle(ambiguous.GetProperty("errors").EnumerateArray());
        Assert.AreEqual(("series_id", "series_id.ambiguous"), (error.GetProperty("field").GetString(), error.GetProperty("code").GetString()));
        Assert.AreEqual(2, error.GetProperty("allowed").GetArrayLength());

        var (onlyStatus, only) = await AskAsync(host, "query_metric", new JsonObject { ["metric_name"] = "canonical.number", ["raw"] = true,
            ["matchers"] = new JsonArray(new JsonObject { ["key"] = "http.request.method", ["op"] = "eq", ["value"] = "GET" }) });
        Assert.AreEqual(HttpStatusCode.OK, onlyStatus, only.GetRawText());
        Assert.AreEqual("GET", Assert.ContainsSingle(only.GetProperty("attributes").EnumerateArray()).GetProperty("value").GetString());
        Assert.AreEqual("1", Assert.ContainsSingle(only.GetProperty("points").EnumerateArray()).GetProperty("data").GetProperty("value").GetProperty("value").GetString());

        var (missingStatus, missing) = await AskAsync(host, "query_metric", new JsonObject { ["metric_name"] = "canonical.number", ["raw"] = true, ["series_id"] = "absent" });
        Assert.AreEqual(HttpStatusCode.NotFound, missingStatus, missing.GetRawText());
        Assert.AreEqual(("series", "absent"), (missing.GetProperty("resource_type").GetString(), missing.GetProperty("resource_id").GetString()));
    }

    // One analysis question over the second the canonical points were recorded in.
    private static async Task<(HttpStatusCode Status, JsonElement Body)> AskAsync(CollectorApiTestHost host, string operation, JsonObject input)
    {
        input["start_time"] = "2023-11-14T22:13:19Z";
        input["end_time"] = "2023-11-14T22:13:21Z";
        using var response = await host.SendAsync(HttpMethod.Post, "/api/v1/analysis/" + operation, "reader", input.ToJsonString());
        return (response.StatusCode, await host.JsonAsync(response));
    }

    [TestMethod]
    public async Task Actual_v13_database_keeps_historical_projection_and_marks_erased_originals_unknown()
    {
        var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "metrics-v13.2.1");
        var path = Path.Combine(Path.GetTempPath(), $"qyl-v13-upgrade-{Guid.NewGuid():N}.duckdb");
        try
        {
            await using (var connection = new DuckDBConnection($"DataSource={path}"))
            {
                connection.Open();
                await using var command = connection.CreateCommand();
                command.CommandText = await File.ReadAllTextAsync(Path.Combine(fixture, "schema.sql"), Ct);
                command.ExecuteNonQuery();
                command.CommandText = (await File.ReadAllTextAsync(Path.Combine(fixture, "load.sql"), Ct)).Replace("__FIXTURE_DIRECTORY__", fixture.Replace("'", "''", StringComparison.Ordinal), StringComparison.Ordinal);
                command.ExecuteNonQuery();
            }
            for (var reopen = 0; reopen < 2; reopen++)
            {
                await using var store = new DuckDbStore(path);
                var series = Assert.ContainsSingle(await store.ListMetricSeriesAsync("default", "historical.counter", [], ct: Ct));
                var item = Assert.ContainsSingle(await Read(store, series, "default"));
                var raw = MetricMapper.ToContract(item, series);
                Assert.IsNull(raw.Data); Assert.IsNull(raw.Flags); Assert.IsNull(raw.Exemplars);
                Assert.IsNull(raw.ResourceDroppedAttributesCount); Assert.IsNull(raw.ScopeDroppedAttributesCount);
                Assert.AreEqual("12121212121212121212121212121212", Assert.ContainsSingle(raw.HistoricalExemplarTraceIds!));
                Assert.AreEqual(9_007_199_254_740_992d, raw.Projection!.Value);
                using var json = JsonDocument.Parse(JsonSerializer.Serialize(raw, global::Qyl.Api.Contracts.ContractTypes.TypeInfo<global::Qyl.Api.Contracts.OTel.Metrics.MetricPoint>()));
                Assert.AreEqual(JsonValueKind.Null, json.RootElement.GetProperty("data").ValueKind);
            }
        }
        finally { Delete(path); }
    }

    [TestMethod]
    public async Task Missing_derived_rows_are_recovered_from_original_observations_on_reopen()
    {
        var path = Path.Combine(Path.GetTempPath(), $"qyl-projection-recovery-{Guid.NewGuid():N}.duckdb");
        try
        {
            await using (var store = new DuckDbStore(path)) await Insert(store, [Number(long.MaxValue)]);
            await using (var connection = new DuckDBConnection($"DataSource={path}"))
            {
                connection.Open();
                await using var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM metric_points; DELETE FROM metric_exemplars";
                command.ExecuteNonQuery();
            }
            await using var reopened = new DuckDbStore(path);
            var series = Assert.ContainsSingle(await reopened.ListMetricSeriesAsync("alpha", "canonical.number", [], ct: Ct));
            var raw = MetricMapper.ToContract(Assert.ContainsSingle(await Read(reopened, series)), series);
            Assert.AreEqual(long.MaxValue, Assert.IsInstanceOfType<AttributeIntValue>(Assert.IsInstanceOfType<Contract.MetricNumberPointData>(raw.Data).Value).Value);
            var projection = Assert.ContainsSingle(Assert.ContainsSingle(await reopened.QueryMetricAsync(Range(series), Ct)).Points);
            Assert.IsNotNull(projection.Value);
        }
        finally { Delete(path); }
    }

    [TestMethod]
    public async Task Retention_removes_canonical_observations_and_all_derived_rows()
    {
        await using var store = new DuckDbStore(":memory:");
        await Insert(store, [Number(1), Number(2) with { TimeUnixNano = Time + 10 }]);
        var series = Assert.ContainsSingle(await store.ListMetricSeriesAsync("alpha", "canonical.number", [], ct: Ct));
        await store.DeleteExpiredMetricsBatchAsync(Time + 1, 10, Ct);
        var remaining = Assert.ContainsSingle(await Read(store, series));
        Assert.AreEqual(Time + 10, remaining.Observation.TimeUnixNano);
        await store.DeleteExpiredMetricsBatchAsync(Time + 11, 10, Ct);
        Assert.IsEmpty(await Read(store, series));
        Assert.IsEmpty(await store.ListMetricSeriesAsync("alpha", "canonical.number", [], ct: Ct));
    }

    private Task<IReadOnlyList<MetricObservationPageItem>> Read(DuckDbStore store, MetricSeriesRow series, string project = "alpha") =>
        store.ListMetricPointsAsync(new MetricPointQuery { ProjectId = project, MetricName = series.MetricName, SeriesId = series.SeriesId, Limit = 1000 }, Ct);

    private async Task Insert(DuckDbStore store, IReadOnlyList<MetricPointIngestionRecord> points)
    {
        var rows = MetricStorageMapper.ToStorageRows(new MetricIngestionBatch(points, 0, null), "alpha");
        await store.InsertMetricsAsync(rows.Series, rows.Observations, Ct);
    }

    private static MetricRangeQuery Range(MetricSeriesRow series) => new()
    {
        ProjectId = "alpha", MetricName = series.MetricName, SeriesId = series.SeriesId,
        StartUnixNano = Time - 1, EndUnixNano = Time + 100, StepUnixNano = 1000,
        Aggregation = MetricAggregation.Sum, SeriesLimit = 10
    };
    private static MetricPointIngestionRecord Number(long value) => new()
    {
        MetricName = "canonical.number", Kind = Kind.Gauge, Temporality = Temporality.Unspecified, IsMonotonic = false,
        ServiceName = "canonical-tests", TimeUnixNano = Time, Attributes = new Dictionary<string, OtlpAttributeValue>(),
        ResourceAttributes = new Dictionary<string, OtlpAttributeValue>(),
        Data = new Contract.MetricNumberPointData { Value = new AttributeIntValue { Value = value } }
    };
    private static ExportMetricsServiceRequest Request(Metric metric)
    {
        var scope = new ScopeMetrics(); scope.Metrics.Add(metric);
        var resource = new ResourceMetrics(); resource.ScopeMetrics.Add(scope);
        var request = new ExportMetricsServiceRequest(); request.ResourceMetrics.Add(resource);
        return request;
    }

    private static void Delete(string path) { File.Delete(path); File.Delete(path + ".wal"); }
}
