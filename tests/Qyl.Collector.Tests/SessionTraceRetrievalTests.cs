using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests;

public sealed class SessionTraceRetrievalTests
{
    [Fact]
    public async Task Session_query_returns_the_full_trace_when_only_one_non_root_span_carries_the_session_id()
    {
        await using var store = new DuckDbStore(":memory:");

        // Trace 1: root + two children; ONLY one child span is tagged with session.id.
        var taggedTraceId = TraceIdBytes(1);
        var request = new ExportTraceServiceRequest
        {
            ResourceSpans =
            {
                new ResourceSpans
                {
                    Resource = new Resource(),
                    ScopeSpans =
                    {
                        new ScopeSpans
                        {
                            Spans =
                            {
                                Span(taggedTraceId, spanId: 1, name: "root", startNano: 1),
                                Span(taggedTraceId, spanId: 2, name: "request-handler", startNano: 2,
                                    parentSpanId: 1, sessionId: "sess-1"),
                                Span(taggedTraceId, spanId: 3, name: "gen_ai.chat", startNano: 3,
                                    parentSpanId: 2)
                            }
                        }
                    }
                }
            }
        };

        // Trace 2: unrelated, no session id anywhere.
        var untaggedTraceId = TraceIdBytes(2);
        request.ResourceSpans[0].ScopeSpans[0].Spans.Add(
            Span(untaggedTraceId, spanId: 4, name: "unrelated", startNano: 4));

        await store.EnqueueAsync(ToBatch(request), TestContext.Current.CancellationToken);

        var spans = await store.GetSpansBySessionAsync("sess-1", "default", TestContext.Current.CancellationToken);

        var taggedHex = Convert.ToHexString(taggedTraceId).ToLowerInvariant();
        Assert.Equal(3, spans.Count);
        Assert.All(spans, s => Assert.Equal(taggedHex, s.TraceId));
        Assert.Equal(["root", "request-handler", "gen_ai.chat"], spans.Select(s => s.Name));
    }

    [Fact]
    public async Task Untagged_telemetry_is_still_addressable_by_its_trace_id_session_key()
    {
        await using var store = new DuckDbStore(":memory:");

        var traceId = TraceIdBytes(7);
        var request = new ExportTraceServiceRequest
        {
            ResourceSpans =
            {
                new ResourceSpans
                {
                    Resource = new Resource(),
                    ScopeSpans =
                    {
                        new ScopeSpans
                        {
                            Spans =
                            {
                                Span(traceId, spanId: 1, name: "root", startNano: 1),
                                Span(traceId, spanId: 2, name: "child", startNano: 2, parentSpanId: 1)
                            }
                        }
                    }
                }
            }
        };

        await store.EnqueueAsync(ToBatch(request), TestContext.Current.CancellationToken);

        // NULL session_id falls back to trace_id as the session key.
        var sessionKey = Convert.ToHexString(traceId).ToLowerInvariant();
        var spans = await store.GetSpansBySessionAsync(sessionKey, "default", TestContext.Current.CancellationToken);

        Assert.Equal(2, spans.Count);
        Assert.All(spans, s => Assert.Equal(sessionKey, s.TraceId));
    }

    private static byte[] TraceIdBytes(byte identity) => [.. new byte[15], identity];

    private static Span Span(
        byte[] traceId,
        byte spanId,
        string name,
        ulong startNano,
        byte? parentSpanId = null,
        string? sessionId = null)
    {
        var span = new Span
        {
            TraceId = ByteString.CopyFrom(traceId),
            SpanId = ByteString.CopyFrom([.. new byte[7], spanId]),
            Name = name,
            StartTimeUnixNano = startNano,
            EndTimeUnixNano = startNano + 1
        };
        if (parentSpanId is { } parent)
            span.ParentSpanId = ByteString.CopyFrom([.. new byte[7], parent]);
        if (sessionId is not null)
            span.Attributes.Add(new KeyValue
            {
                Key = "session.id",
                Value = new AnyValue { StringValue = sessionId }
            });
        return span;
    }

    private static SpanBatch ToBatch(ExportTraceServiceRequest request) =>
        new(IngestionStorageMapper.ToSpanStorageRows(OtlpConverter.ConvertTraceRequest(request)));
}
