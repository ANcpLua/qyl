using System.Text.Json;
using Google.Protobuf;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;
using Qyl.Api.Contracts.Common.Pagination;
using Qyl.Collector.Hosting;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests;

[TestClass]
public sealed class TracePagingTests(TestContext testContext)
{
    [TestMethod]
    public async Task Equal_activity_times_page_by_trace_id_without_duplicates_or_gaps()
    {
        await using var store = new DuckDbStore(":memory:");
        var request = new ExportTraceServiceRequest
        {
            ResourceSpans =
            {
                Trace(1, (10, 100)),
                Trace(2, (20, 100)),
                Trace(3, (30, 100))
            }
        };
        var rows = IngestionStorageMapper.ToSpanStorageRows(OtlpConverter.ConvertTraceRequest(request));
        await store.EnqueueAsync(new SpanBatch(rows), testContext.CancellationToken);

        var traceIds = new List<string>();
        string? cursor = null;
        do
        {
            var query = "?limit=1" + (cursor is null ? string.Empty : "&cursor=" + Uri.EscapeDataString(cursor));
            var context = CreateContext(query);
            var result = await CollectorEndpointExtensions.GetTracesAsync(
                context,
                store,
                testContext.CancellationToken);
            await result.ExecuteAsync(context);
            var page = await ReadPageAsync(context);

            traceIds.Add(Assert.ContainsSingle(page.Items).TraceId);
            cursor = page.NextCursor;
            Assert.AreEqual(cursor is not null, page.HasMore);
        } while (cursor is not null);

        Assert.HasCount(3, traceIds);
        Assert.HasCount(3, traceIds.Distinct(StringComparer.Ordinal));
    }

    [TestMethod]
    public async Task Trace_limit_pages_complete_traces_and_emits_a_stable_next_cursor()
    {
        await using var store = new DuckDbStore(":memory:");
        var request = new ExportTraceServiceRequest
        {
            ResourceSpans =
            {
                Trace(1, (100, 300), (120, 250), (140, 240)),
                Trace(2, (50, 200), (75, 175)),
                Trace(3, (25, 100))
            }
        };
        var rows = IngestionStorageMapper.ToSpanStorageRows(OtlpConverter.ConvertTraceRequest(request));
        await store.EnqueueAsync(new SpanBatch(rows), testContext.CancellationToken);

        var firstContext = CreateContext("?limit=2");
        var firstResult = await CollectorEndpointExtensions.GetTracesAsync(
            firstContext,
            store,
            testContext.CancellationToken);
        await firstResult.ExecuteAsync(firstContext);
        var first = await ReadPageAsync(firstContext);

        Assert.HasCount(2, first.Items);
        Assert.IsTrue(first.HasMore);
        Assert.IsFalse(string.IsNullOrWhiteSpace(first.NextCursor));
        Assert.AreEqual(3, first.Items[0].SpanCount);
        Assert.HasCount(3, first.Items[0].Spans);
        Assert.AreEqual<ulong>(200, first.Items[0].DurationNs);
        Assert.AreEqual(2, first.Items[1].SpanCount);
        Assert.HasCount(2, first.Items[1].Spans);

        var secondContext = CreateContext(
            "?limit=2&cursor=" + Uri.EscapeDataString(first.NextCursor!));
        var secondResult = await CollectorEndpointExtensions.GetTracesAsync(
            secondContext,
            store,
            testContext.CancellationToken);
        await secondResult.ExecuteAsync(secondContext);
        var second = await ReadPageAsync(secondContext);

        var remaining = Assert.ContainsSingle(second.Items);
        Assert.AreEqual(1, remaining.SpanCount);
        Assert.ContainsSingle(remaining.Spans);
        Assert.IsFalse(second.HasMore);
        Assert.IsNull(second.NextCursor);
        Assert.DoesNotContain(trace => first.Items.Any(previous => previous.TraceId == trace.TraceId),second.Items);
    }

    private static ResourceSpans Trace(byte identity, params (ulong Start, ulong End)[] timings)
    {
        var traceId = ByteString.CopyFrom([.. Enumerable.Repeat(identity, 16)]);
        var rootSpanId = ByteString.CopyFrom([.. Enumerable.Repeat(identity, 8)]);
        var scopeSpans = new ScopeSpans();
        for (var index = 0; index < timings.Length; index++)
        {
            var (start, end) = timings[index];
            scopeSpans.Spans.Add(new Span
            {
                TraceId = traceId,
                SpanId = index is 0
                    ? rootSpanId
                    : ByteString.CopyFrom([.. Enumerable.Repeat((byte)(identity + index), 8)]),
                ParentSpanId = index is 0 ? ByteString.Empty : rootSpanId,
                Name = $"trace-{identity}-span-{index}",
                StartTimeUnixNano = start,
                EndTimeUnixNano = end
            });
        }

        return new ResourceSpans
        {
            Resource = new Resource(),
            ScopeSpans = { scopeSpans }
        };
    }

    private static DefaultHttpContext CreateContext(string query)
    {
        return new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddLogging()
                .ConfigureHttpJsonOptions(static options =>
                    options.SerializerOptions.TypeInfoResolverChain.Insert(0, QylSerializerContext.Default))
                .BuildServiceProvider(),
            Request =
            {
                QueryString = new QueryString(query)
            },
            Response =
            {
                Body = new MemoryStream()
            }
        };
    }

    private async Task<CursorPageTrace> ReadPageAsync(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        var page = await JsonSerializer.DeserializeAsync(
            context.Response.Body,
            QylSerializerContext.Default.CursorPageTrace,
            testContext.CancellationToken);
        return Assert.IsExactInstanceOfType<CursorPageTrace>(page);
    }
}
