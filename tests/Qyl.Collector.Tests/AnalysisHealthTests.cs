using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Trace.V1;
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
    /// A semantic-convention gap is a required attribute the producer did not send. The collector does
    /// not persist cicd.pipeline.result (its key holds a denied token), so whether a producer sent it is
    /// unknown and never a gap; a missing url.scheme is.
    /// </summary>
    [TestMethod]
    public async Task A_required_attribute_the_collector_does_not_persist_is_not_a_gap()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var start = FixtureUnixNano - 30 * Minute;
        OtlpSpan Server(byte id, string name, params OpenTelemetry.Proto.Common.V1.KeyValue[] attributes)
        {
            var span = new OtlpSpan
            {
                TraceId = ByteString.CopyFrom([.. Enumerable.Repeat(id, 16)]), SpanId = ByteString.CopyFrom([.. Enumerable.Repeat(id, 8)]),
                Name = name, Kind = OtlpSpan.Types.SpanKind.Server, StartTimeUnixNano = start, EndTimeUnixNano = start + 1_000_000
            };
            span.Attributes.Add(attributes);
            return span;
        }
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
                                Server(1, "RUN qyl-ci", OtlpTestSpans.String("cicd.pipeline.name", "qyl-ci"), OtlpTestSpans.String("cicd.pipeline.run.id", "42"),
                                    OtlpTestSpans.String("cicd.pipeline.result", "success")),
                                Server(2, "GET /", OtlpTestSpans.String("http.request.method", "GET"), OtlpTestSpans.String("url.path", "/"))
                            }
                        }
                    }
                }
            }
        });

        var gaps = (await Read(host, "check_telemetry_health", "{}")).GetProperty("semconv_gaps").EnumerateArray()
            .Select(static gap => gap.GetProperty("operation").GetString() + ":" + gap.GetProperty("attribute").GetString());

        Assert.AreSequenceEqual(["GET /:url.scheme"], gaps);
    }
}
