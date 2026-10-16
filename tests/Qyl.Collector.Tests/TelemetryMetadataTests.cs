using MetricContract = Qyl.Api.Contracts.OTel.Metrics;
using CommonContract = Qyl.Api.Contracts.Common;
using Google.Protobuf;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Ingestion.Primitives;
using Qyl.Collector.Mapping;
using Qyl.Collector.Storage;
using WireSpan = OpenTelemetry.Proto.Trace.V1.Span;

namespace Qyl.Collector.Tests;

[TestClass]
public sealed class TelemetryMetadataTests(TestContext testContext)
{
    private static readonly string[] s_expectedLogOrder = ["newest", "event", "oldest"];
    private const ulong Time = 1_700_000_000_000_000_000;
    private const string ResourceSchema = "https://opentelemetry.io/schemas/1.44.0";
    private const string ScopeSchema = "https://example.test/instrumentation/2";
    private static readonly ByteString TraceId = ByteString.CopyFrom(Enumerable.Repeat((byte)0x12, 16).ToArray());
    private static readonly ByteString SpanId = ByteString.CopyFrom(Enumerable.Repeat((byte)0x34, 8).ToArray());


    [TestMethod]
    public async Task Span_metadata_survives_database_reopen_and_contract_projection()
    {
        var request = new ExportTraceServiceRequest
        {
            ResourceSpans =
            {
                new ResourceSpans
                {
                    SchemaUrl = ResourceSchema,
                    Resource = Resource(),
                    ScopeSpans =
                    {
                        new ScopeSpans
                        {
                            Scope = Scope(), SchemaUrl = ScopeSchema,
                            Spans =
                            {
                                new WireSpan
                                {
                                    TraceId = TraceId, SpanId = SpanId, Name = "operation",
                                    StartTimeUnixNano = Time, EndTimeUnixNano = Time + 1,
                                    TraceState = "tenant=blue", Flags = uint.MaxValue,
                                    DroppedAttributesCount = 3, DroppedEventsCount = 5, DroppedLinksCount = 7,
                                    Events = { new WireSpan.Types.Event { Name = "exception", TimeUnixNano = Time,
                                        DroppedAttributesCount = 11, Attributes = { Attribute("exception.type", "ExpectedException") } } },
                                    Links = { new WireSpan.Types.Link { TraceId = TraceId, SpanId = SpanId,
                                        TraceState = "linked=green", Flags = uint.MaxValue, DroppedAttributesCount = 13 } }
                                }
                            }
                        }
                    }
                }
            }
        };
        var path = Path.Combine(Path.GetTempPath(), $"qyl-metadata-{Guid.NewGuid():N}.duckdb");
        try
        {
            await using (var store = new DuckDbStore(path))
                await store.EnqueueAsync(new SpanBatch(IngestionStorageMapper.ToSpanStorageRows(
                    OtlpConverter.ConvertTraceRequest(request), "metadata")), testContext.CancellationToken);

            await using var reopened = new DuckDbStore(path);
            var row = Assert.ContainsSingle(await reopened.GetSpansAsync("metadata", ct: testContext.CancellationToken));
            var span = Assert.ContainsSingle(SpanMapper.ToContracts([row]));
            Assert.AreEqual("tenant=blue", span.TraceState);
            Assert.AreEqual(uint.MaxValue, span.Flags);
            Assert.AreEqual(3U, span.DroppedAttributesCount);
            Assert.AreEqual(5U, span.DroppedEventsCount);
            Assert.AreEqual(7U, span.DroppedLinksCount);
            Assert.AreEqual(ResourceSchema, span.Resource.SchemaUrl);
            Assert.AreEqual(17U, span.Resource.DroppedAttributesCount);
            Assert.IsNotNull(span.InstrumentationScope);
            Assert.AreEqual("build/main", span.InstrumentationScope.ScopeVersion);
            Assert.AreEqual(ScopeSchema, span.InstrumentationScope.SchemaUrl);
            Assert.AreEqual(19U, span.InstrumentationScope.DroppedAttributesCount);
            Assert.AreEqual("instrumentation.build", Assert.ContainsSingle(span.InstrumentationScope.ScopeAttributes!).Key);
            var e = Assert.ContainsSingle(span.Events!);
            Assert.AreEqual(11U, e.DroppedAttributesCount);
            Assert.AreEqual("exception.type", Assert.ContainsSingle(e.Attributes!).Key);
            var link = Assert.ContainsSingle(span.Links!);
            Assert.AreEqual("linked=green", link.TraceState);
            Assert.AreEqual(uint.MaxValue, link.Flags);
            Assert.AreEqual(13U, link.DroppedAttributesCount);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".wal");
        }
    }

    [TestMethod]
    public async Task Logs_keep_raw_severity_body_scope_and_trace_flags()
    {
        var request = LogRequest(new LogRecord
        {
            TimeUnixNano = Time,
            ObservedTimeUnixNano = Time + 99,
            SeverityNumber = SeverityNumber.Warn2,
            SeverityText = "NOTICE/remote",
            TraceId = TraceId,
            SpanId = SpanId,
            Flags = uint.MaxValue,
            DroppedAttributesCount = 23,
            Body = new AnyValue { BytesValue = ByteString.CopyFrom([0, 128, 255]) },
            EventName = "worker.ready"
        });
        await using var store = new DuckDbStore(":memory:");
        var rows = IngestionStorageMapper.ToLogStorageRows(OtlpConverter.ConvertLogs(request, TimeProvider.System), "metadata");
        await store.InsertLogsAsync(rows, testContext.CancellationToken);
        var row = Assert.ContainsSingle(await store.GetLogsAsync("metadata", ct: testContext.CancellationToken));
        var log = LogMapper.ToContract(row);
        Assert.AreEqual("NOTICE/remote", log.SeverityText);
        Assert.AreEqual(Time + 99, log.ObservedTimeUnixNano);
        Assert.AreEqual(uint.MaxValue, log.Flags);
        Assert.AreEqual(23U, log.DroppedAttributesCount);
        Assert.AreEqual(ResourceSchema, log.Resource.SchemaUrl);
        Assert.AreEqual(17U, log.Resource.DroppedAttributesCount);
        Assert.IsNotNull(log.InstrumentationScope);
        Assert.AreEqual("build/main", log.InstrumentationScope.ScopeVersion);
        Assert.AreEqual(ScopeSchema, log.InstrumentationScope.SchemaUrl);
        Assert.AreEqual(19U, log.InstrumentationScope.DroppedAttributesCount);
        Assert.AreEqual("instrumentation.build", Assert.ContainsSingle(log.InstrumentationScope.ScopeAttributes!).Key);
        var body = Assert.IsExactInstanceOfType<Qyl.Api.Contracts.Common.AttributeValue.ObjectValue>(log.Body);
        var bytes = Assert.IsExactInstanceOfType<Qyl.Api.Contracts.Common.AttributeBytesValue>(body.Value);
        Assert.AreSequenceEqual(new byte[] { 0, 128, 255 }, bytes.Base64);
    }

    [TestMethod]
    public void Absent_log_observation_uses_receive_time_and_preserves_absent_severity()
    {
        var instant = DateTimeOffset.FromUnixTimeMilliseconds(1_900_000_000_123).AddTicks(4567);
        var clock = new FixedTimeProvider(instant);
        var log = Assert.ContainsSingle(OtlpConverter.ConvertLogs(LogRequest(new LogRecord { TimeUnixNano = Time }), clock).Logs);
        Assert.AreEqual(Qyl.Collector.Ingestion.Primitives.TimeConversions.ToUnixNanoUnsigned(instant), log.ObservedTimeUnixNano);
        Assert.AreNotEqual(Time, log.ObservedTimeUnixNano);
        Assert.AreEqual("", LogMapper.ToContract(Assert.ContainsSingle(IngestionStorageMapper.ToLogStorageRows(new([log])))).SeverityText);
    }

    [TestMethod]
    public void Collector_composition_preserves_the_injected_clock()
    {
        var clock = new FixedTimeProvider(DateTimeOffset.UnixEpoch);
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(clock);
        Qyl.Collector.Hosting.CollectorCoreExtensions.AddQylCollectorCore(services,
            new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        Assert.AreSame(clock, provider.GetRequiredService<TimeProvider>());
    }

    [TestMethod]
    [DataRow(0UL)]
    [DataRow(Time)]
    public async Task Historical_unknown_observation_time_remains_unknown(ulong eventTime)
    {
        await using var store = new DuckDbStore(":memory:");
        await store.InsertLogsAsync([new LogStorageRow
        {
            ProjectId = "metadata", LogId = "historical", TimeUnixNano = eventTime,
            ObservedTimeUnixNano = null, SeverityNumber = 17, Body = "\"historical\""
        }], testContext.CancellationToken);
        var stored = Assert.ContainsSingle(await store.GetLogsAsync("metadata", ct: testContext.CancellationToken));
        Assert.IsNull(stored.ObservedTimeUnixNano);
        Assert.AreEqual(eventTime == 0 ? (ulong?)null : eventTime, stored.TimeUnixNano);
        Assert.AreEqual(eventTime == 0 ? (ulong?)null : eventTime, stored.EffectiveTimeUnixNano());
        var contract = LogMapper.ToContract(stored);
        Assert.IsNull(contract.ObservedTimeUnixNano);
        Assert.AreEqual(eventTime == 0 ? (ulong?)null : eventTime, contract.TimeUnixNano);
    }

    [TestMethod]
    [DataRow("name")]
    [DataRow("version")]
    [DataRow("schema")]
    [DataRow("attributes")]
    [DataRow("resource-schema")]
    public void Instrumentation_and_resource_identity_keep_independent_metric_streams(string dimension)
    {
        var first = Point();
        var second = dimension switch
        {
            "name" => first with { ScopeName = "library-b" },
            "version" => first with { ScopeVersion = "build/next" },
            "schema" => first with { ScopeSchemaUrl = "https://example.test/next" },
            "attributes" => first with { ScopeAttributes = new Dictionary<string, OtlpAttributeValue> { ["build"] = OtlpAttributeValue.FromInt(2) } },
            "resource-schema" => first with { ResourceSchemaUrl = "https://example.test/resource/next" },
            _ => throw new ArgumentOutOfRangeException(nameof(dimension))
        };
        var rows = MetricStorageMapper.ToStorageRows(new([first, second], 0, null), "metadata");
        Assert.HasCount(2, rows.Series);
        Assert.HasCount(2, rows.Observations);
        Assert.AreAllDistinct(rows.Series.Select(static row => row.SeriesId));
    }

    [TestMethod]
    public async Task Missing_event_timestamps_use_observation_for_filtering_and_pagination()
    {
        await using var host = await CollectorApiTestHost.StartAsync(testContext.CancellationToken);
        var start = (ulong)TimeProvider.System.GetUtcNow().AddMinutes(-1).ToUnixTimeMilliseconds() * 1_000_000;
        await host.Store.InsertLogsAsync(
        [
            new LogStorageRow { ProjectId = "alpha", LogId = "newest", TimeUnixNano = 0,
                ObservedTimeUnixNano = start + 30_000_000, SeverityNumber = 17, Body = "\"newest\"" },
            new LogStorageRow { ProjectId = "alpha", LogId = "event", TimeUnixNano = start + 20_000_000,
                ObservedTimeUnixNano = start + 40_000_000, SeverityNumber = 17, Body = "\"event\"" },
            new LogStorageRow { ProjectId = "alpha", LogId = "oldest", TimeUnixNano = 0,
                ObservedTimeUnixNano = start + 10_000_000, SeverityNumber = 17, Body = "\"oldest\"" }
        ], testContext.CancellationToken);
        var since = Uri.EscapeDataString(DateTimeOffset.FromUnixTimeMilliseconds((long)(start / 1_000_000)).ToString("O"));
        var path = $"/api/v1/logs?start_time={since}&limit=1";
        var bodies = new List<string?>();
        for (var page = 0; page < 3; page++)
        {
            using var response = await host.SendAsync(HttpMethod.Get, path, "reader");
            Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
            var json = await host.JsonAsync(response);
            var log = Assert.ContainsSingle(json.GetProperty("items").EnumerateArray());
            bodies.Add(log.GetProperty("body").GetString());
            if (page is 0)
            {
                Assert.IsFalse(log.TryGetProperty("time_unix_nano", out _));
                Assert.AreEqual((start + 30_000_000).ToString(System.Globalization.CultureInfo.InvariantCulture), log.GetProperty("observed_time_unix_nano").GetString());
            }
            if (page < 2)
                path = $"/api/v1/logs?start_time={since}&limit=1&cursor={Uri.EscapeDataString(json.GetProperty("next_cursor").GetString()!)}";
            else
                Assert.IsFalse(json.TryGetProperty("next_cursor", out var cursor) && cursor.ValueKind != System.Text.Json.JsonValueKind.Null);
        }
        Assert.AreSequenceEqual(s_expectedLogOrder, bodies);
    }

    [TestMethod]
    public async Task Receiver_observation_time_does_not_duplicate_an_export_retry()
    {
        var first = Assert.ContainsSingle(OtlpConverter.ConvertLogs(LogRequest(new LogRecord { TimeUnixNano = Time }), TimeProvider.System).Logs);
        var retry = first with { ObservedTimeUnixNano = first.ObservedTimeUnixNano + 1_000_000 };
        var firstRow = Assert.ContainsSingle(IngestionStorageMapper.ToLogStorageRows(new([first]), "metadata"));
        var retryRow = Assert.ContainsSingle(IngestionStorageMapper.ToLogStorageRows(new([retry]), "metadata"));
        Assert.AreEqual(firstRow.LogId, retryRow.LogId);
        await using var store = new DuckDbStore(":memory:");
        await store.InsertLogsAsync([firstRow], testContext.CancellationToken);
        await store.InsertLogsAsync([retryRow], testContext.CancellationToken);
        var stored = Assert.ContainsSingle(await store.GetLogsAsync("metadata", ct: testContext.CancellationToken));
        Assert.AreEqual(first.ObservedTimeUnixNano, stored.ObservedTimeUnixNano);
    }

    [TestMethod]
    public async Task Metric_wire_metadata_survives_storage_and_contract_projection()
    {
        var request = new OpenTelemetry.Proto.Collector.Metrics.V1.ExportMetricsServiceRequest
        {
            ResourceMetrics =
            {
                new OpenTelemetry.Proto.Metrics.V1.ResourceMetrics
                {
                    Resource = Resource(), SchemaUrl = ResourceSchema,
                    ScopeMetrics =
                    {
                        new OpenTelemetry.Proto.Metrics.V1.ScopeMetrics
                        {
                            Scope = Scope(), SchemaUrl = ScopeSchema,
                            Metrics =
                            {
                                new OpenTelemetry.Proto.Metrics.V1.Metric
                                {
                                    Name = "requests",
                                    Gauge = new OpenTelemetry.Proto.Metrics.V1.Gauge
                                    {
                                        DataPoints = { new OpenTelemetry.Proto.Metrics.V1.NumberDataPoint { TimeUnixNano = Time, AsInt = 42 } }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
        var writes = MetricStorageMapper.ToStorageRows(OtlpConverter.ConvertMetrics(request), "metadata");
        await using var store = new DuckDbStore(":memory:");
        await store.InsertMetricsAsync(writes.Series, writes.Observations, testContext.CancellationToken);
        var series = MetricMapper.ToContract(Assert.ContainsSingle(await store.ListMetricSeriesAsync(
            "metadata", "requests", [], ct: testContext.CancellationToken)));
        Assert.IsNotNull(series.Resource);
        Assert.AreEqual(ResourceSchema, series.Resource.SchemaUrl);
        Assert.AreEqual(17U, series.Resource.DroppedAttributesCount);
        Assert.IsNotNull(series.InstrumentationScope);
        Assert.AreEqual("library-a", series.InstrumentationScope.ScopeName);
        Assert.AreEqual("build/main", series.InstrumentationScope.ScopeVersion);
        Assert.AreEqual(ScopeSchema, series.InstrumentationScope.SchemaUrl);
        Assert.AreEqual(19U, series.InstrumentationScope.DroppedAttributesCount);
        Assert.AreEqual("instrumentation.build", Assert.ContainsSingle(series.InstrumentationScope.ScopeAttributes!).Key);
    }

    [TestMethod]
    public void Changing_dropped_counts_does_not_change_metric_identity()
    {
        var first = Point();
        var second = first with { ScopeDroppedAttributesCount = 3, ResourceDroppedAttributesCount = 5 };
        var rows = MetricStorageMapper.ToStorageRows(new([first, second], 0, null), "metadata");
        var series = Assert.ContainsSingle(rows.Series);
        Assert.HasCount(2, rows.Observations);
        Assert.HasCount(1, rows.Observations.Select(static observation => observation.MeasurementId).Distinct());
        Assert.AreEqual(3U, series.ScopeDroppedAttributesCount);
        Assert.AreEqual(5U, series.ResourceDroppedAttributesCount);
    }

    [TestMethod]
    public async Task Delta_rate_does_not_invent_an_interval_for_an_unknown_start()
    {
        await using var host = await CollectorApiTestHost.StartAsync(testContext.CancellationToken);
        var start = TimeProvider.System.GetUtcNow().AddSeconds(-5);
        var startNanos = (ulong)start.ToUnixTimeMilliseconds() * 1_000_000;
        var unknown = Point() with
        {
            Kind = MetricKind.Sum,
            Temporality = MetricTemporality.Delta,
            IsMonotonic = true,
            TimeUnixNano = startNanos + 100_000_000,
            Data = Number(1)
        };
        var known = unknown with { TimeUnixNano = startNanos + 1_100_000_000, StartTimeUnixNano = startNanos + 1_000_000_000 };
        var rows = MetricStorageMapper.ToStorageRows(new([unknown, known], 0, null), "alpha");
        await host.Store.InsertMetricsAsync(rows.Series, rows.Observations, testContext.CancellationToken);
        var body = $$"""{"metric_name":"requests","function":"rate","step_ms":1000,"start_time":"{{start:O}}","end_time":"{{start.AddSeconds(2):O}}"}""";
        using var response = await host.SendAsync(HttpMethod.Post, "/api/v1/analysis/query_metric", "reader", body);
        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
        var json = await host.JsonAsync(response);
        var buckets = Assert.ContainsSingle(json.GetProperty("series").EnumerateArray()).GetProperty("buckets");
        Assert.AreEqual(System.Text.Json.JsonValueKind.Null, buckets[0].GetProperty("value").ValueKind);
        Assert.AreEqual(10d, buckets[1].GetProperty("value").GetDouble(), 0.0000000005d);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task Historical_metric_identity_migrates_points_and_exemplars_atomically(bool existingIdentity, bool distinctScope)
    {
        var path = Path.Combine(Path.GetTempPath(), $"qyl-metric-migration-{Guid.NewGuid():N}.duckdb");
        try
        {
            var historicalPoint = distinctScope ? Point() with
            {
                ScopeName = null, ScopeVersion = null, ScopeSchemaUrl = null,
                ScopeAttributes = new Dictionary<string, OtlpAttributeValue>()
            } : Point();
            historicalPoint = historicalPoint with { Exemplars = [new MetricContract.MetricExemplar
            {
                TimeUnixNano = Time, Value = new CommonContract.AttributeDoubleValue { Value = 1 },
                FilteredAttributes = [], TraceId = new string('a', 32)
            }] };
            var writes = MetricStorageMapper.ToStorageRows(new([historicalPoint], 0, null), "metadata");
            var current = Assert.ContainsSingle(writes.Series);
            var point = Assert.ContainsSingle(writes.Observations);
            await using (var store = new DuckDbStore(path))
            {
                await store.InsertMetricsAsync([current with { SeriesId = "historical" }],
                    [point with { SeriesId = "historical" }], testContext.CancellationToken);
                if (existingIdentity)
                {
                    var next = distinctScope ? MetricStorageMapper.ToStorageRows(new([Point()], 0, null), "metadata") : writes;
                    await store.InsertMetricsAsync(next.Series, next.Observations, ct: testContext.CancellationToken);
                }
            }
            using (var connection = new DuckDB.NET.Data.DuckDBConnection($"DataSource={path}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "UPDATE metric_series SET instrument_id = NULL WHERE series_id = 'historical'";
                command.ExecuteNonQuery();
            }
            await using var reopened = new DuckDbStore(path);
            var series = await reopened.ListMetricSeriesAsync("metadata", "requests", [], ct: testContext.CancellationToken);
            Assert.HasCount(distinctScope ? 2 : 1, series);
            var migrated = Assert.ContainsSingle(row => row.SeriesId == current.SeriesId, series);
            Assert.AreEqual(historicalPoint.ScopeName, migrated.ScopeName);
            var snapshot = await reopened.ReadAnalysisSnapshotAsync("metadata", ct: testContext.CancellationToken);
            Assert.HasCount(distinctScope ? 2 : 1, snapshot.Points);
            Assert.AreEqual(current.SeriesId, Assert.ContainsSingle(row => row.SeriesId == current.SeriesId, snapshot.Points).SeriesId);
            Assert.AreEqual(current.SeriesId, Assert.ContainsSingle(snapshot.Exemplars).SeriesId);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task Conflicting_observations_survive_series_identity_migration_without_overwriting_samples()
    {
        var path = Path.Combine(Path.GetTempPath(), $"qyl-metric-conflict-{Guid.NewGuid():N}.duckdb");
        try
        {
            var writes = MetricStorageMapper.ToStorageRows(new([Point()], 0, null), "metadata");
            var other = MetricStorageMapper.ToStorageRows(new([Point() with { Data = Number(999) }], 0, null), "metadata");
            var current = Assert.ContainsSingle(writes.Series);
            await using (var store = new DuckDbStore(path))
            {
                await store.InsertMetricsAsync(writes.Series, writes.Observations, testContext.CancellationToken);
                await store.InsertMetricsAsync([current with { SeriesId = "historical" }],
                    [Assert.ContainsSingle(other.Observations) with { SeriesId = "historical" }], testContext.CancellationToken);
            }
            using (var connection = new DuckDB.NET.Data.DuckDBConnection($"DataSource={path}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "UPDATE metric_series SET instrument_id = NULL WHERE series_id = 'historical'";
                command.ExecuteNonQuery();
            }
            await using var reopened = new DuckDbStore(path);
            var observations = await reopened.ListMetricPointsAsync(new MetricPointQuery
            {
                ProjectId = "metadata", MetricName = "requests", SeriesId = current.SeriesId, Limit = 10
            }, testContext.CancellationToken);
            Assert.HasCount(2, observations);
            Assert.IsTrue(observations.All(static item => item.HasConflict));
            Assert.AreSequenceEqual(new double?[] { 1, 999 }, observations.Select(static item => MetricProjection.Project(item.Observation, false).Value).Order());
            await Assert.ThrowsExactlyAsync<MetricQueryConflictException>(() => reopened.QueryMetricAsync(new MetricRangeQuery
            {
                ProjectId = "metadata", MetricName = "requests", SeriesId = current.SeriesId,
                StartUnixNano = Time - 1, EndUnixNano = Time + 1, StepUnixNano = 1
            }, testContext.CancellationToken));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task Cumulative_rate_uses_the_actual_submillisecond_interval()
    {
        await using var host = await CollectorApiTestHost.StartAsync(testContext.CancellationToken);
        var start = TimeProvider.System.GetUtcNow().AddSeconds(-5);
        var startNanos = (ulong)start.ToUnixTimeMilliseconds() * 1_000_000;
        var first = Point() with
        {
            Kind = MetricKind.Sum, Temporality = MetricTemporality.Cumulative, IsMonotonic = true,
            TimeUnixNano = startNanos + 100_000_000, StartTimeUnixNano = startNanos, Data = Number(1)
        };
        var second = first with { TimeUnixNano = first.TimeUnixNano + 100_000, Data = Number(2) };
        var rows = MetricStorageMapper.ToStorageRows(new([first, second], 0, null), "alpha");
        await host.Store.InsertMetricsAsync(rows.Series, rows.Observations, testContext.CancellationToken);
        var body = $$"""{"metric_name":"requests","function":"rate","step_ms":1000,"start_time":"{{start:O}}","end_time":"{{start.AddSeconds(1):O}}"}""";
        using var response = await host.SendAsync(HttpMethod.Post, "/api/v1/analysis/query_metric", "reader", body);
        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
        var json = await host.JsonAsync(response);
        var buckets = Assert.ContainsSingle(json.GetProperty("series").EnumerateArray()).GetProperty("buckets");
        Assert.AreEqual(10_000d, buckets[0].GetProperty("value").GetDouble(), 0.0000000005d);
    }

    [TestMethod]
    public async Task Same_metric_name_in_two_scopes_produces_two_paged_catalog_entries()
    {
        await using var host = await CollectorApiTestHost.StartAsync(testContext.CancellationToken);
        var first = Point() with { TimeUnixNano = Qyl.Collector.Ingestion.Primitives.TimeConversions.ToUnixNanoUnsigned(TimeProvider.System.GetUtcNow()) };
        var second = first with { ScopeName = "library-b" };
        var writes = MetricStorageMapper.ToStorageRows(new([first, second], 0, null), "alpha");
        await host.Store.InsertMetricsAsync(writes.Series, writes.Observations, testContext.CancellationToken);
        Assert.HasCount(2, await host.Store.ListMetricsAsync("alpha", ct: testContext.CancellationToken));
        using var analysis = await host.SendAsync(HttpMethod.Post, "/api/v1/analysis/list_metrics", "reader", "{\"metric_name\":\"requests\"}");
        Assert.AreEqual(System.Net.HttpStatusCode.OK, analysis.StatusCode);
        Assert.AreEqual(2, (await host.JsonAsync(analysis)).GetProperty("items").GetArrayLength());
        using var firstPage = await host.SendAsync(HttpMethod.Get, "/api/v1/metrics?name_prefix=requests&limit=1", "reader");
        Assert.AreEqual(System.Net.HttpStatusCode.OK, firstPage.StatusCode);
        var firstJson = await host.JsonAsync(firstPage);
        var firstItem = Assert.ContainsSingle(firstJson.GetProperty("items").EnumerateArray());
        using var nextPage = await host.SendAsync(HttpMethod.Get,
            "/api/v1/metrics?name_prefix=requests&limit=1&cursor=" + Uri.EscapeDataString(firstJson.GetProperty("next_cursor").GetString()!), "reader");
        Assert.AreEqual(System.Net.HttpStatusCode.OK, nextPage.StatusCode);
        var nextJson = await host.JsonAsync(nextPage);
        var nextItem = Assert.ContainsSingle(nextJson.GetProperty("items").EnumerateArray());
        Assert.AreEqual("requests", firstItem.GetProperty("name").GetString());
        Assert.AreEqual("requests", nextItem.GetProperty("name").GetString());
        Assert.AreNotEqual(firstItem.GetProperty("instrument_id").GetString(), nextItem.GetProperty("instrument_id").GetString());
        Assert.AreNotEqual(firstItem.GetProperty("instrumentation_scope").GetProperty("name").GetString(),
            nextItem.GetProperty("instrumentation_scope").GetProperty("name").GetString());
        Assert.IsFalse(nextJson.GetProperty("has_more").GetBoolean());
    }

    [TestMethod]
    public void Integer_and_double_points_of_one_instrument_share_identity()
    {
        var metric = new OpenTelemetry.Proto.Metrics.V1.Metric
        {
            Name = "numeric.representation",
            Gauge = new OpenTelemetry.Proto.Metrics.V1.Gauge
            {
                DataPoints = { new OpenTelemetry.Proto.Metrics.V1.NumberDataPoint { TimeUnixNano = Time, AsInt = 42 }, new OpenTelemetry.Proto.Metrics.V1.NumberDataPoint { TimeUnixNano = Time + 1, AsDouble = 43 } }
            }
        };
        var rows = MetricStorageMapper.ToStorageRows(OtlpConverter.ConvertMetrics(MetricRequest(metric)), "metadata");
        Assert.HasCount(1, rows.Series);
        Assert.HasCount(2, rows.Observations);
        Assert.AreSequenceEqual(new double?[] { 42, 43 }, rows.Observations.OrderBy(static point => point.TimeUnixNano)
            .Select(static point => MetricProjection.Project(point, false).Value));
    }

    [TestMethod]
    public async Task Description_changes_keep_one_identity_and_the_longer_description()
    {
        var first = Point() with { Description = "long descriptive instrument text" };
        var shortened = first with { Description = "z", TimeUnixNano = Time + 1 };
        var sameBatch = MetricStorageMapper.ToStorageRows(new([first, shortened], 0, null), "metadata");
        Assert.AreEqual(first.Description, Assert.ContainsSingle(sameBatch.Series).Description);
        await using var store = new DuckDbStore(":memory:");
        var original = MetricStorageMapper.ToStorageRows(new([first], 0, null), "metadata");
        var update = MetricStorageMapper.ToStorageRows(new([shortened], 0, null), "metadata");
        await store.InsertMetricsAsync(original.Series, original.Observations, testContext.CancellationToken);
        await store.InsertMetricsAsync(update.Series, update.Observations, testContext.CancellationToken);
        var entry = Assert.ContainsSingle(await store.ListMetricsAsync("metadata", ct: testContext.CancellationToken));
        Assert.AreEqual(first.Description, entry.Description);
        Assert.AreEqual(1L, entry.SeriesCount);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public void Zero_metric_end_time_is_reported_as_rejected_and_unknown_start_remains_unknown(int kind)
    {
        var metric = new OpenTelemetry.Proto.Metrics.V1.Metric { Name = "time.required" };
        switch (kind)
        {
            case 0:
                metric.Gauge = new OpenTelemetry.Proto.Metrics.V1.Gauge
                { DataPoints = { new OpenTelemetry.Proto.Metrics.V1.NumberDataPoint { AsInt = 1 }, new OpenTelemetry.Proto.Metrics.V1.NumberDataPoint { AsInt = 2, TimeUnixNano = Time } } }; break;
            case 1:
                metric.Sum = new OpenTelemetry.Proto.Metrics.V1.Sum
                { DataPoints = { new OpenTelemetry.Proto.Metrics.V1.NumberDataPoint { AsInt = 1 }, new OpenTelemetry.Proto.Metrics.V1.NumberDataPoint { AsInt = 2, TimeUnixNano = Time } } }; break;
            case 2:
                metric.Histogram = new OpenTelemetry.Proto.Metrics.V1.Histogram
                { DataPoints = { new OpenTelemetry.Proto.Metrics.V1.HistogramDataPoint(), new OpenTelemetry.Proto.Metrics.V1.HistogramDataPoint { TimeUnixNano = Time } } }; break;
            default:
                metric.ExponentialHistogram = new OpenTelemetry.Proto.Metrics.V1.ExponentialHistogram
                { DataPoints = { new OpenTelemetry.Proto.Metrics.V1.ExponentialHistogramDataPoint(), new OpenTelemetry.Proto.Metrics.V1.ExponentialHistogramDataPoint { TimeUnixNano = Time } } }; break;
        }
        var batch = OtlpConverter.ConvertMetrics(MetricRequest(metric));
        Assert.AreEqual(1L, batch.RejectedDataPoints);
        Assert.IsNotNull(batch.RejectionMessage);
        Assert.Contains("time_unix_nano", batch.RejectionMessage);
        var point = Assert.ContainsSingle(batch.Points);
        Assert.AreEqual(Time, point.TimeUnixNano);
        Assert.IsNull(point.StartTimeUnixNano);
    }

    [TestMethod]
    public async Task Metric_http_queries_reject_ambiguous_variants_and_select_an_exact_series()
    {
        await using var host = await CollectorApiTestHost.StartAsync(testContext.CancellationToken);
        var timestamp = TimeProvider.System.GetUtcNow().AddSeconds(-10);
        var nanos = (ulong)timestamp.ToUnixTimeMilliseconds() * 1_000_000;
        var first = Point() with { TimeUnixNano = nanos };
        var second = first with
        {
            Kind = MetricKind.Sum,
            Temporality = MetricTemporality.Cumulative,
            IsMonotonic = true,
            Unit = "{item}",
            Data = Number(99)
        };
        var writes = MetricStorageMapper.ToStorageRows(new([first, second], 0, null), "alpha");
        await host.Store.InsertMetricsAsync(writes.Series, writes.Observations, testContext.CancellationToken);
        var start = Uri.EscapeDataString(timestamp.AddSeconds(-1).ToString("O"));
        var end = Uri.EscapeDataString(timestamp.AddSeconds(1).ToString("O"));
        var path = $"/api/v1/metrics/requests/query?start_time={start}&end_time={end}&aggregation=last";
        using (var ambiguous = await host.SendAsync(HttpMethod.Get, path, "reader"))
        {
            Assert.AreEqual(System.Net.HttpStatusCode.Conflict, ambiguous.StatusCode);
            var problem = await host.JsonAsync(ambiguous);
            Assert.Contains("series_id", problem.GetProperty("detail").GetString()!);
        }
        var selected = Assert.ContainsSingle(writes.Series.Where(static row => row.Unit == "{item}"));
        using (var response = await host.SendAsync(HttpMethod.Get, path + "&series_id=" + selected.SeriesId, "reader"))
        {
            Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
            var json = await host.JsonAsync(response);
            Assert.AreEqual("{item}", json.GetProperty("unit").GetString());
            var series = Assert.ContainsSingle(json.GetProperty("series").EnumerateArray());
            Assert.AreEqual(99d, Assert.ContainsSingle(series.GetProperty("buckets").EnumerateArray()).GetProperty("value").GetDouble());
        }
        using var missing = await host.SendAsync(HttpMethod.Get, path + "&series_id=ms_00000000000000000000000000000000", "reader");
        Assert.AreEqual(System.Net.HttpStatusCode.NotFound, missing.StatusCode);
    }

    [TestMethod]
    public async Task Incompatible_instruments_preserve_both_values_and_report_actual_descriptors()
    {
        var first = Point();
        var second = first with
        {
            Kind = MetricKind.Sum,
            Temporality = MetricTemporality.Cumulative,
            IsMonotonic = true,
            Unit = "{item}",
            Data = Number(99)
        };
        var writes = MetricStorageMapper.ToStorageRows(new([first, second], 0, null), "metadata");
        await using var store = new DuckDbStore(":memory:");
        await store.InsertMetricsAsync(writes.Series, writes.Observations, testContext.CancellationToken);
        Assert.HasCount(2, await store.ListMetricSeriesAsync("metadata", first.MetricName, [], ct: testContext.CancellationToken));
        var descriptor = MetricMapper.ToContract(Assert.ContainsSingle(await store.ListMetricsAsync("metadata", ct: testContext.CancellationToken)));
        Assert.HasCount(2, descriptor.Conflicts!);
        Assert.IsTrue(descriptor.Conflicts!.Any(static conflict => conflict.Kind == Qyl.Api.Contracts.OTel.Metrics.MetricKind.Gauge && !conflict.Monotonic));
        Assert.IsTrue(descriptor.Conflicts!.Any(static conflict => conflict.Kind == Qyl.Api.Contracts.OTel.Metrics.MetricKind.Sum && conflict.Monotonic && conflict.Unit == "{item}"));
        var query = new MetricRangeQuery
        {
            ProjectId = "metadata",
            MetricName = first.MetricName,
            StartUnixNano = Time,
            EndUnixNano = Time + 1,
            StepUnixNano = 1,
            Aggregation = MetricAggregation.Last
        };
        await Assert.ThrowsExactlyAsync<MetricQueryConflictException>(() => store.QueryMetricAsync(query, testContext.CancellationToken));
        var selected = Assert.ContainsSingle(writes.Series.Where(static row => row.MetricKind == (byte)MetricKind.Gauge));
        var selectedResult = Assert.ContainsSingle(await store.QueryMetricAsync(query with { SeriesId = selected.SeriesId }, testContext.CancellationToken));
        Assert.AreEqual(1d, Assert.ContainsSingle(selectedResult.Points).Value);
    }

    private static Resource Resource() => new()
    {
        DroppedAttributesCount = 17,
        Attributes = { Attribute("service.name", "metadata-service") }
    };

    private static InstrumentationScope Scope() => new()
    {
        Name = "library-a",
        Version = "build/main",
        DroppedAttributesCount = 19,
        Attributes = { Attribute("instrumentation.build", "abc123") }
    };

    private static KeyValue Attribute(string key, string value) => new() { Key = key, Value = new AnyValue { StringValue = value } };

    private static ExportLogsServiceRequest LogRequest(LogRecord log) => new()
    {
        ResourceLogs = { new ResourceLogs { Resource = Resource(), SchemaUrl = ResourceSchema,
            ScopeLogs = { new ScopeLogs { Scope = Scope(), SchemaUrl = ScopeSchema, LogRecords = { log } } } } }
    };

    private static OpenTelemetry.Proto.Collector.Metrics.V1.ExportMetricsServiceRequest MetricRequest(OpenTelemetry.Proto.Metrics.V1.Metric metric) => new()
    {
        ResourceMetrics = { new OpenTelemetry.Proto.Metrics.V1.ResourceMetrics { Resource = Resource(), SchemaUrl = ResourceSchema,
            ScopeMetrics = { new OpenTelemetry.Proto.Metrics.V1.ScopeMetrics { Scope = Scope(), SchemaUrl = ScopeSchema, Metrics = { metric } } } } }
    };

    private static MetricContract.MetricNumberPointData Number(double value) =>
        new() { Value = new CommonContract.AttributeDoubleValue { Value = value } };

    private static MetricPointIngestionRecord Point() => new()
    {
        MetricName = "requests",
        Kind = MetricKind.Gauge,
        Temporality = MetricTemporality.Unspecified,
        IsMonotonic = false,
        ServiceName = "metadata-service",
        ScopeName = "library-a",
        ScopeVersion = "build/main",
        ScopeSchemaUrl = ScopeSchema,
        ResourceSchemaUrl = ResourceSchema,
        TimeUnixNano = Time,
        Data = Number(1),
        Attributes = new Dictionary<string, OtlpAttributeValue>(),
        ResourceAttributes = new Dictionary<string, OtlpAttributeValue>(),
        ScopeAttributes = new Dictionary<string, OtlpAttributeValue> { ["build"] = OtlpAttributeValue.FromInt(1) }
    };
    private sealed class FixedTimeProvider(DateTimeOffset instant) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instant;
    }

}
