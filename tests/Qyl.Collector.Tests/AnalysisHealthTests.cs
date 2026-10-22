using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Resource.V1;
using static Qyl.Collector.Tests.CollectorApiTestHost;
using OtlpResourceSpans = OpenTelemetry.Proto.Trace.V1.ResourceSpans;
using OtlpScopeSpans = OpenTelemetry.Proto.Trace.V1.ScopeSpans;
using OtlpSpan = OpenTelemetry.Proto.Trace.V1.Span;

namespace Qyl.Collector.Tests;

/// <summary>
/// check_telemetry_health reports gaps in what producers sent, not the boundaries of what qyl can see.
/// </summary>
public sealed partial class AnalysisApiTests
{
    /// <summary>
    /// A span whose producer recorded its parent as remote (OTLP span flags bits 8 and 9) was called from
    /// another process, which need not report to qyl, so its absent parent does not orphan it. A parent known
    /// to be local, or of unknown locality, is missing.
    /// </summary>
    [TestMethod]
    public async Task A_span_whose_parent_is_remote_is_not_an_orphan()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var start = FixtureUnixNano - 30 * Minute;
        OtlpSpan Called(byte id, uint flags) => new()
        {
            TraceId = ByteString.CopyFrom([.. Enumerable.Repeat(id, 16)]), SpanId = ByteString.CopyFrom([.. Enumerable.Repeat(id, 8)]),
            ParentSpanId = ByteString.CopyFrom([.. Enumerable.Repeat((byte)(id + 100), 8)]), Name = "tools/call", Kind = OtlpSpan.Types.SpanKind.Server,
            StartTimeUnixNano = start, EndTimeUnixNano = start + 1_000_000, Flags = flags
        };
        await host.IngestTracesAsync(new ExportTraceServiceRequest
        {
            ResourceSpans =
            {
                new OtlpResourceSpans
                {
                    Resource = new Resource { Attributes = { OtlpTestSpans.String("service.name", "mcp") } },
                    ScopeSpans = { new OtlpScopeSpans { Spans = { Called(1, 0x301), Called(2, 0x101), Called(3, 0x001) } } }
                }
            }
        });

        var orphans = (await Read(host, "check_telemetry_health", "{}")).GetProperty("orphan_spans");

        Assert.AreEqual(2, orphans.GetProperty("count").GetInt32());
        Assert.AreEqual(2d / 3, orphans.GetProperty("share").GetDouble(), 1e-9);
    }

    /// <summary>
    /// A semantic-convention gap is a required attribute the producer did not send. The collector does not
    /// persist url.full, so whether an HTTP client sent it is unknown and never a gap. It keeps
    /// cicd.pipeline.result, an enum attribute the denied-token rule exempts, so of two pipeline runs the
    /// one without it is a gap.
    /// </summary>
    [TestMethod]
    public async Task A_required_attribute_is_a_gap_only_when_the_collector_keeps_it()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var start = FixtureUnixNano - 30 * Minute;
        OtlpSpan Exported(byte id, OtlpSpan.Types.SpanKind kind, string name, params KeyValue[] attributes)
        {
            var span = new OtlpSpan
            {
                TraceId = ByteString.CopyFrom([.. Enumerable.Repeat(id, 16)]), SpanId = ByteString.CopyFrom([.. Enumerable.Repeat(id, 8)]),
                Name = name, Kind = kind, StartTimeUnixNano = start, EndTimeUnixNano = start + 1_000_000
            };
            span.Attributes.Add(attributes);
            return span;
        }
        KeyValue[] Run(string id) => [OtlpTestSpans.String("cicd.pipeline.name", "qyl-ci"), OtlpTestSpans.String("cicd.pipeline.run.id", id)];
        await host.IngestTracesAsync(new ExportTraceServiceRequest
        {
            ResourceSpans =
            {
                new OtlpResourceSpans
                {
                    Resource = new Resource { Attributes = { OtlpTestSpans.String("service.name", "ci") } },
                    ScopeSpans =
                    {
                        new OtlpScopeSpans
                        {
                            Spans =
                            {
                                Exported(1, OtlpSpan.Types.SpanKind.Server, "RUN qyl-ci", [.. Run("41"), OtlpTestSpans.String("cicd.pipeline.result", "success")]),
                                Exported(2, OtlpSpan.Types.SpanKind.Server, "RUN qyl-ci", Run("42")),
                                Exported(3, OtlpSpan.Types.SpanKind.Client, "GET", OtlpTestSpans.String("http.request.method", "GET"),
                                    OtlpTestSpans.String("server.address", "api.example.com"), OtlpTestSpans.Int("server.port", 443),
                                    OtlpTestSpans.String("url.full", "https://api.example.com/orders"))
                            }
                        }
                    }
                }
            }
        });

        var gap = Assert.ContainsSingle((await Read(host, "check_telemetry_health", "{}")).GetProperty("semconv_gaps").EnumerateArray());

        Assert.AreEqual("RUN qyl-ci:cicd.pipeline.result", gap.GetProperty("operation").GetString() + ":" + gap.GetProperty("attribute").GetString());
        Assert.AreEqual(0.5, gap.GetProperty("missing_share").GetDouble());
    }

    /// <summary>
    /// Ingestion lag is the time from a producer's observation to the Collector's receipt. A record that
    /// happened 20 minutes ago and carries no observed time says nothing about it; two observed 2 and 4
    /// seconds before receipt average 3 seconds.
    /// </summary>
    [TestMethod]
    public async Task Ingestion_lag_runs_from_the_producers_observation_to_receipt()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        LogRecord Record(ulong time, ulong observed) => new()
        {
            TimeUnixNano = time, ObservedTimeUnixNano = observed, SeverityNumber = SeverityNumber.Info, Body = new AnyValue { StringValue = "entry" }
        };
        using var response = await host.SendProtobufAsync("/v1/logs", new ExportLogsServiceRequest
        {
            ResourceLogs =
            {
                new ResourceLogs
                {
                    Resource = new Resource { Attributes = { OtlpTestSpans.String("service.name", "worker") } },
                    ScopeLogs = { new ScopeLogs { LogRecords = { Record(FixtureUnixNano - 2 * Second, FixtureUnixNano - 2 * Second), Record(FixtureUnixNano - 4 * Second, FixtureUnixNano - 4 * Second), Record(FixtureUnixNano - 20 * Minute, 0) } } }
                }
            }
        }, "ingester");
        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);

        var health = await Read(host, "check_telemetry_health", "{}");

        Assert.AreEqual(3000d, health.GetProperty("ingestion_lag_ms").GetDouble(), 1e-6);
        Assert.IsFalse(health.TryGetProperty("retention", out _));
        Assert.AreEqual(FixtureUtcNow.AddMinutes(-20), health.GetProperty("data_span").GetProperty("oldest").GetDateTimeOffset());
        Assert.AreEqual(20d / 1440, health.GetProperty("data_span").GetProperty("days").GetDouble(), 1e-9);
    }

    /// <summary>A service that sent nothing in the window or its baseline is not found, never reported healthy.</summary>
    [TestMethod]
    public async Task Health_of_an_unknown_service_is_not_found()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        await host.Store.EnqueueAsync(new Storage.SpanBatch([Span(TraceId(1), SpanId(1), FixtureUnixNano - 10 * Minute, FixtureUnixNano - 10 * Minute + 1000)]), testContext.CancellationToken);

        using var response = await host.SendAsync(HttpMethod.Post, "/api/v1/analysis/check_telemetry_health", ReaderApiKey, """{"service_name":"nope"}""");
        var json = await host.JsonAsync(response);

        Assert.AreEqual(System.Net.HttpStatusCode.NotFound, response.StatusCode, json.GetRawText());
        Assert.Contains("nope", json.GetRawText());
        using var known = await host.SendAsync(HttpMethod.Post, "/api/v1/analysis/check_telemetry_health", ReaderApiKey, """{"service_name":"checkout"}""");
        Assert.AreEqual(System.Net.HttpStatusCode.OK, known.StatusCode);
    }

    /// <summary>
    /// data_span reaches back over what the rest of the report covers: without service_name every service but qyl's own
    /// MCP server, which exports into the project it reads, and with service_name that service alone, its logs included.
    /// </summary>
    [TestMethod]
    public async Task Data_span_reaches_back_over_the_services_the_report_covers()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var mcp = Api.Contracts.Mcp.Tools.QylServiceValues.Mcp;
        await host.Store.EnqueueAsync(new Storage.SpanBatch([
            Span(TraceId(1), SpanId(1), FixtureUnixNano - 50 * Minute, FixtureUnixNano - 50 * Minute + 1000, service: mcp),
            Span(TraceId(2), SpanId(2), FixtureUnixNano - 45 * Minute, FixtureUnixNano - 45 * Minute + 1000, service: "worker"),
            Span(TraceId(3), SpanId(3), FixtureUnixNano - 30 * Minute, FixtureUnixNano - 30 * Minute + 1000)
        ]), testContext.CancellationToken);
        await host.Store.InsertLogsAsync([new Storage.LogStorageRow
        {
            ProjectId = AlphaProjectId, LogId = "checkout-entry", TimeUnixNano = FixtureUnixNano - 40 * Minute, SeverityNumber = 9, ServiceName = "checkout", Body = "entry"
        }], testContext.CancellationToken);
        async Task<DateTimeOffset> OldestAsync(string input) =>
            (await Read(host, "check_telemetry_health", input)).GetProperty("data_span").GetProperty("oldest").GetDateTimeOffset();

        Assert.AreEqual(FixtureUtcNow.AddMinutes(-45), await OldestAsync("{}"));
        Assert.AreEqual(FixtureUtcNow.AddMinutes(-50), await OldestAsync($$"""{"service_name":"{{mcp}}"}"""));
        Assert.AreEqual(FixtureUtcNow.AddMinutes(-40), await OldestAsync("""{"service_name":"checkout"}"""));
    }
}
