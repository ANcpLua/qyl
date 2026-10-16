using System.Net;
using System.Text.Json.Nodes;
using DuckDB.NET.Data;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Grpc.Core;
using Grpc.Net.Client;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;
using Qyl.Collector.Hosting;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Storage;
using WireSpan = OpenTelemetry.Proto.Trace.V1.Span;

namespace Qyl.Collector.Tests;

[TestClass]
public sealed class OtlpRecordValidationTests(TestContext testContext)
{
    private const ulong Time = 1_700_000_000_000_000_000;
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Mixed_trace_export_retains_valid_spans_and_reports_each_rejected_item(bool grpc)
    {
        var request = new ExportTraceServiceRequest();
        var resource = new ResourceSpans(); var scope = new ScopeSpans();
        scope.Spans.Add(Span(1)); scope.Spans.Add(Bounds(Span(2), 0, 1));
        var zeroTrace = Span(3); zeroTrace.TraceId = ByteString.CopyFrom(new byte[16]); scope.Spans.Add(zeroTrace);
        var zeroSpan = Span(4); zeroSpan.SpanId = ByteString.CopyFrom(new byte[8]); scope.Spans.Add(zeroSpan);
        var shortId = Span(5); shortId.TraceId = ByteString.CopyFrom([1]); scope.Spans.Add(shortId);
        scope.Spans.Add(Bounds(Span(6), Time, Time - 1));
        scope.Spans.Add(Bounds(Span(7), 0, 0));
        var badAttributes = Span(8); badAttributes.Attributes.Add(new KeyValue { Key = "", Value = new AnyValue { StringValue = "invalid" } }); scope.Spans.Add(badAttributes);
        resource.ScopeSpans.Add(scope);
        var invalidScope = new ScopeSpans { Scope = new InstrumentationScope { Attributes = { Text("duplicate", "a"), Text("duplicate", "b") } } };
        invalidScope.Spans.Add(Span(9)); resource.ScopeSpans.Add(invalidScope); request.ResourceSpans.Add(resource);
        var invalidResource = new ResourceSpans { Resource = new Resource { Attributes = { Text("service.name", "a"), Text("service.name", "b") } } };
        invalidResource.ScopeSpans.Add(new ScopeSpans { Spans = { Span(10) } }); request.ResourceSpans.Add(invalidResource);
        await using var host = await CollectorApiTestHost.StartAsync(Ct);
        ExportTraceServiceResponse response;
        if (grpc)
        {
            using var channel = GrpcChannel.ForAddress(host.GrpcAddress);
            response = await channel.CreateCallInvoker().AsyncUnaryCall(
                Method(TraceServiceReflection.Descriptor.Services[0], ExportTraceServiceRequest.Parser, ExportTraceServiceResponse.Parser),
                null, new CallOptions(Headers(), cancellationToken: Ct), request);
        }
        else
        {
            using var http = await host.SendProtobufAsync("/v1/traces", request, "ingester");
            Assert.AreEqual(HttpStatusCode.OK, http.StatusCode);
            response = ExportTraceServiceResponse.Parser.ParseFrom(await http.Content.ReadAsByteArrayAsync(Ct));
        }
        Assert.AreEqual(8L, response.PartialSuccess.RejectedSpans);
        Assert.Contains("trace_id", response.PartialSuccess.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("end >= start", response.PartialSuccess.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("unique", response.PartialSuccess.ErrorMessage, StringComparison.Ordinal);
        var stored = await host.Store.GetSpansAsync("alpha", ct: Ct);
        Assert.HasCount(2, stored);
        var epoch = Assert.ContainsSingle(static span => span.StartTimeUnixNano == 0, stored);
        Assert.AreEqual(1UL, epoch.EndTimeUnixNano); Assert.AreEqual(1UL, epoch.DurationNs);

    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Mixed_log_export_keeps_valid_records_and_reports_invalid_records_over_both_transports(bool grpc)
    {
        var request = Logs(new LogRecord { TimeUnixNano = Time, Body = new AnyValue { StringValue = "valid" } },
            new LogRecord { TimeUnixNano = Time, TraceId = ByteString.CopyFrom(new byte[16]) },
            new LogRecord { TimeUnixNano = Time, Attributes = { Text("", "invalid") } },
            new LogRecord { TimeUnixNano = Time, Attributes = { Text("http.route", "/a"), Text("http.route", "/b") } });
        var bad = new ResourceLogs { Resource = new Resource { Attributes = { Text("service.name", "a"), Text("service.name", "b") } } };
        bad.ScopeLogs.Add(new ScopeLogs { LogRecords = { new LogRecord { TimeUnixNano = Time } } }); request.ResourceLogs.Add(bad);
        await using var host = await CollectorApiTestHost.StartAsync(Ct);
        ExportLogsServiceResponse response;
        if (grpc)
        {
            using var channel = GrpcChannel.ForAddress(host.GrpcAddress);
            response = await channel.CreateCallInvoker().AsyncUnaryCall(
                Method(LogsServiceReflection.Descriptor.Services[0], ExportLogsServiceRequest.Parser, ExportLogsServiceResponse.Parser),
                null, new CallOptions(Headers(), cancellationToken: Ct), request);
        }
        else
        {
            using var http = await host.SendProtobufAsync("/v1/logs", request, "ingester");
            Assert.AreEqual(HttpStatusCode.OK, http.StatusCode);
            response = ExportLogsServiceResponse.Parser.ParseFrom(await http.Content.ReadAsByteArrayAsync(Ct));
        }
        Assert.AreEqual(4L, response.PartialSuccess.RejectedLogRecords);
        Assert.IsFalse(string.IsNullOrWhiteSpace(response.PartialSuccess.ErrorMessage));
        Assert.AreEqual("\"valid\"", Assert.ContainsSingle(await host.Store.GetLogsAsync("alpha", ct: Ct)).Body);
    }

    [TestMethod]
    public async Task Untimed_identical_records_keep_request_multiplicity_and_retries_deduplicate()
    {
        var clock = new FixedTime(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000));
        var request = Logs(new LogRecord { Body = new AnyValue { StringValue = "same" } }, new LogRecord { Body = new AnyValue { StringValue = "same" } });
        var first = IngestionStorageMapper.ToLogStorageRows(OtlpConverter.ConvertLogs(request, clock), "alpha");
        Assert.HasCount(2, first); Assert.AreNotEqual(first[0].LogId, first[1].LogId);
        Assert.IsTrue(first.All(static row => row.TimeUnixNano is null && row.ObservedTimeUnixNano == Time));
        await using var store = new DuckDbStore(":memory:");
        await store.InsertLogsAsync(first, Ct);
        await store.InsertLogsAsync(IngestionStorageMapper.ToLogStorageRows(OtlpConverter.ConvertLogs(request,
            new FixedTime(clock.GetUtcNow().AddHours(1))), "alpha"), Ct);
        var saved = await store.GetLogsAsync("alpha", ct: Ct); Assert.HasCount(2, saved);
        Assert.IsTrue(saved.All(static row => row.ObservedTimeUnixNano == Time));
    }

    [TestMethod]
    public async Task Observation_only_logs_are_found_by_analysis_and_unknown_historical_rows_page_without_epoch_times()
    {
        await using var host = await CollectorApiTestHost.StartAsync(Ct);
        var request = Logs(new LogRecord { Body = new AnyValue { StringValue = "observed" } });
        await host.Store.InsertLogsAsync(IngestionStorageMapper.ToLogStorageRows(OtlpConverter.ConvertLogs(request,
            new FixedTime(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000))), "alpha"), Ct);
        for (var i = 0; i < 3; i++) await host.Store.InsertLogsAsync([new LogStorageRow { ProjectId = "alpha", LogId = "unknown-" + i,
            SeverityNumber = 17, Body = "\"unknown-" + i + "\"" }], Ct);
        using var analysis = await host.SendAsync(HttpMethod.Post, "/api/v1/analysis/search_logs", "reader",
            new JsonObject { ["start_time"] = "2023-11-14T22:13:19Z", ["end_time"] = "2023-11-14T22:13:21Z" }.ToJsonString());
        Assert.AreEqual(HttpStatusCode.OK, analysis.StatusCode);
        var found = await host.JsonAsync(analysis);
        Assert.AreEqual(1, found.GetProperty("logs").GetArrayLength());
        var ids = new HashSet<string>(StringComparer.Ordinal); string? cursor = null;
        do
        {
            using var page = await host.SendAsync(HttpMethod.Get, "/api/v1/logs?limit=1" + (cursor is null ? "" : "&cursor=" + Uri.EscapeDataString(cursor)), "reader");
            Assert.AreEqual(HttpStatusCode.OK, page.StatusCode); var body = await host.JsonAsync(page);
            var item = Assert.ContainsSingle(body.GetProperty("items").EnumerateArray());
            Assert.IsFalse(item.TryGetProperty("time_unix_nano", out _));
            var identity = item.GetProperty("body").GetString()!;
            Assert.IsTrue(ids.Add(identity));
            cursor = body.TryGetProperty("next_cursor", out var next) ? next.GetString() : null;
        } while (cursor is not null);
        Assert.HasCount(4, ids);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct); timeout.CancelAfter(TimeSpan.FromSeconds(5));
        await using var stream = CollectorEndpointExtensions.StreamLogEventsAsync(host.Store, "alpha", null, 17, null, null, timeout.Token).GetAsyncEnumerator(timeout.Token);
        Assert.IsTrue(await stream.MoveNextAsync());
        Assert.IsNull(Assert.IsInstanceOfType<Api.Contracts.Streaming.LogStreamEvent>(stream.Current.Log).Timestamp);
    }

    [TestMethod]
    public async Task Legacy_zero_time_cursor_continues_unknown_history_without_repeating_rows()
    {
        await using var host = await CollectorApiTestHost.StartAsync(Ct);
        await host.Store.InsertLogsAsync(Enumerable.Range(0, 3).Select(static index => new LogStorageRow
        {
            ProjectId = "alpha", LogId = "unknown-" + index, SeverityNumber = 9, Body = "\"unknown\""
        }).ToArray(), Ct);
        var cursor = PageCursorCodec.Encode(PageCursorKind.Logs, PageCursorCodec.Hex(0UL), "unknown-2");
        using var response = await host.SendAsync(HttpMethod.Get,
            "/api/v1/logs?limit=10&cursor=" + Uri.EscapeDataString(cursor), "reader");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var page = await host.JsonAsync(response);
        Assert.AreEqual(2, page.GetProperty("items").GetArrayLength());
        Assert.IsFalse(page.GetProperty("has_more").GetBoolean());
    }

    [TestMethod]
    public async Task Interrupted_nonunique_log_index_rebuild_recovers_on_reopen_without_changing_rows()
    {
        var path = Path.Combine(Path.GetTempPath(), $"qyl-log-index-recovery-{Guid.NewGuid():N}.duckdb");
        try
        {
            await using (var store = new DuckDbStore(path))
                await store.InsertLogsAsync([new LogStorageRow { ProjectId = "default", LogId = "kept", SeverityNumber = 17 }], Ct);
            await using (var connection = new DuckDBConnection($"DataSource={path}"))
            {
                connection.Open();
                await using var transaction = connection.BeginTransaction();
                foreach (var index in DuckDbGeneratedSchema.AuthoritativeTables.Single(static table => table.Name == "logs").Indexes)
                {
                    Assert.IsFalse(index.Unique);
                    await using var drop = connection.CreateCommand();
                    drop.Transaction = transaction;
                    drop.CommandText = $"DROP INDEX \"{index.Name}\"";
                    drop.ExecuteNonQuery();
                }
                transaction.Commit();
            }
            await using (var reopened = new DuckDbStore(path))
                Assert.AreEqual("kept", Assert.ContainsSingle(await reopened.GetLogsAsync("default", ct: Ct)).LogId);
            await using var verified = new DuckDBConnection($"DataSource={path}"); verified.Open();
            await using var query = verified.CreateCommand(); query.CommandText = "SELECT count(*) FROM duckdb_indexes() WHERE schema_name = 'main' AND table_name = 'logs'";
            Assert.AreEqual(DuckDbGeneratedSchema.AuthoritativeTables.Single(static table => table.Name == "logs").Indexes.Length,
                Convert.ToInt32(query.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture));
        }
        finally { File.Delete(path); File.Delete(path + ".wal"); }
    }

    [TestMethod]
    public async Task Original_bodies_with_the_same_retained_prefix_do_not_merge_and_exact_retries_deduplicate()
    {
        var prefix = new string('x', 65_536);
        var request = Logs(new LogRecord { TimeUnixNano = Time, Body = new AnyValue { StringValue = prefix + "a" } },
            new LogRecord { TimeUnixNano = Time, Body = new AnyValue { StringValue = prefix + "b" } });
        var first = IngestionStorageMapper.ToLogStorageRows(OtlpConverter.ConvertLogs(request,
            new FixedTime(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000))), "alpha");
        Assert.AreEqual(first[0].Body, first[1].Body, "The explicit retention policy still keeps the same bounded rendering.");
        await using var store = new DuckDbStore(":memory:");
        await store.InsertLogsAsync(first, Ct);
        await store.InsertLogsAsync(IngestionStorageMapper.ToLogStorageRows(OtlpConverter.ConvertLogs(request,
            new FixedTime(DateTimeOffset.FromUnixTimeSeconds(1_700_000_001))), "alpha"), Ct);
        var saved = await store.GetLogsAsync("alpha", ct: Ct);
        Assert.HasCount(2, saved);
        Assert.AreNotEqual(first[0].LogId, first[1].LogId);
        Assert.IsTrue(saved.All(row => row.Body == first[0].Body));
    }

    [TestMethod]
    public async Task Released_database_unknown_times_migrate_to_null_and_survive_time_retention()
    {
        var path = Path.Combine(Path.GetTempPath(), $"qyl-unknown-log-{Guid.NewGuid():N}.duckdb");
        var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "metrics-v13.2.1");
        try
        {
            await using (var connection = new DuckDBConnection($"DataSource={path}"))
            {
                connection.Open();
                await using var command = connection.CreateCommand();
                command.CommandText = await File.ReadAllTextAsync(Path.Combine(fixture, "schema.sql"), Ct); command.ExecuteNonQuery();
                command.CommandText = (await File.ReadAllTextAsync(Path.Combine(fixture, "load.sql"), Ct)).Replace("__FIXTURE_DIRECTORY__", fixture.Replace("'", "''", StringComparison.Ordinal), StringComparison.Ordinal); command.ExecuteNonQuery();
                command.CommandText = "INSERT INTO logs (project_id, log_id, time_unix_nano, observed_time_unix_nano, severity_number) VALUES ('default', 'unknown', 0, 0, 17), ('default', 'old', 1, 1, 17)"; command.ExecuteNonQuery();
            }
            await using (var store = new DuckDbStore(path))
            {
                var unknown = Assert.ContainsSingle(static row => row.LogId == "unknown", await store.GetLogsAsync("default", ct: Ct));
                Assert.IsNull(unknown.TimeUnixNano); Assert.IsNull(unknown.ObservedTimeUnixNano); Assert.IsNull(unknown.EffectiveTimeUnixNano());
                Assert.AreEqual(1, await store.DeleteExpiredLogsBatchAsync(Time, 100, Ct));
                Assert.AreEqual("unknown", Assert.ContainsSingle(await store.GetLogsAsync("default", ct: Ct)).LogId);
                Assert.IsEmpty(await store.GetLogsAsync("default", start: 0, ct: Ct));
            }
            await using var reopened = new DuckDbStore(path);
            Assert.IsNull(Assert.ContainsSingle(await reopened.GetLogsAsync("default", ct: Ct)).TimeUnixNano);
        }
        finally { File.Delete(path); File.Delete(path + ".wal"); }
    }

    private static ExportLogsServiceRequest Logs(params LogRecord[] logs)
    {
        var scope = new ScopeLogs(); scope.LogRecords.Add(logs);
        return new ExportLogsServiceRequest { ResourceLogs = { new ResourceLogs { ScopeLogs = { scope } } } };
    }
    private static WireSpan Span(byte id) => new()
    {
        TraceId = ByteString.CopyFrom(Enumerable.Repeat(id, 16).ToArray()), SpanId = ByteString.CopyFrom(Enumerable.Repeat(id, 8).ToArray()),
        Name = "valid", StartTimeUnixNano = Time, EndTimeUnixNano = Time + 1
    };
    private static WireSpan Bounds(WireSpan span, ulong start, ulong end)
    { span.StartTimeUnixNano = start; span.EndTimeUnixNano = end; return span; }
    private static KeyValue Text(string key, string value) => new() { Key = key, Value = new AnyValue { StringValue = value } };
    private static Metadata Headers() => new() { { OtlpConstants.ApiKeyHeaderName, "ingester" } };
    private static Method<TRequest, TResponse> Method<TRequest, TResponse>(ServiceDescriptor service, MessageParser<TRequest> request, MessageParser<TResponse> response)
        where TRequest : class, IMessage<TRequest> where TResponse : class, IMessage<TResponse> => new(MethodType.Unary,
            service.FullName, service.Methods[0].Name,
            Marshallers.Create<TRequest>(static value => value.ToByteArray(), request.ParseFrom),
            Marshallers.Create<TResponse>(static value => value.ToByteArray(), response.ParseFrom));
    private sealed class FixedTime(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
}
