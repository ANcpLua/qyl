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

public sealed class TracePagingTests
{
    [Fact]
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
        await store.EnqueueAsync(new SpanBatch(rows), TestContext.Current.CancellationToken);

        var traceIds = new List<string>();
        string? cursor = null;
        do
        {
            var query = "?limit=1" + (cursor is null ? string.Empty : "&cursor=" + Uri.EscapeDataString(cursor));
            var context = CreateContext(query);
            var result = await CollectorEndpointExtensions.GetTracesAsync(
                context,
                store,
                TestContext.Current.CancellationToken);
            await result.ExecuteAsync(context);
            var page = await ReadPageAsync(context);

            traceIds.Add(Assert.Single(page.Items).TraceId);
            cursor = page.NextCursor;
            Assert.Equal(cursor is not null, page.HasMore);
        } while (cursor is not null);

        Assert.Equal(3, traceIds.Count);
        Assert.Equal(3, traceIds.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
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
        await store.EnqueueAsync(new SpanBatch(rows), TestContext.Current.CancellationToken);

        var firstContext = CreateContext("?limit=2");
        var firstResult = await CollectorEndpointExtensions.GetTracesAsync(
            firstContext,
            store,
            TestContext.Current.CancellationToken);
        await firstResult.ExecuteAsync(firstContext);
        var first = await ReadPageAsync(firstContext);

        Assert.Equal(2, first.Items.Count);
        Assert.True(first.HasMore);
        Assert.False(string.IsNullOrWhiteSpace(first.NextCursor));
        Assert.Equal(3, first.Items[0].SpanCount);
        Assert.Equal(3, first.Items[0].Spans.Count);
        Assert.Equal<ulong>(200, first.Items[0].DurationNs);
        Assert.Equal(2, first.Items[1].SpanCount);
        Assert.Equal(2, first.Items[1].Spans.Count);

        var secondContext = CreateContext(
            "?limit=2&cursor=" + Uri.EscapeDataString(first.NextCursor!));
        var secondResult = await CollectorEndpointExtensions.GetTracesAsync(
            secondContext,
            store,
            TestContext.Current.CancellationToken);
        await secondResult.ExecuteAsync(secondContext);
        var second = await ReadPageAsync(secondContext);

        var remaining = Assert.Single(second.Items);
        Assert.Equal(1, remaining.SpanCount);
        Assert.Single(remaining.Spans);
        Assert.False(second.HasMore);
        Assert.Null(second.NextCursor);
        Assert.DoesNotContain(second.Items, trace => first.Items.Any(previous => previous.TraceId == trace.TraceId));
    }

    private static ResourceSpans Trace(byte identity, params (ulong Start, ulong End)[] timings)
    {
        var traceId = ByteString.CopyFrom(Enumerable.Repeat(identity, 16).ToArray());
        var rootSpanId = ByteString.CopyFrom(Enumerable.Repeat(identity, 8).ToArray());
        var scopeSpans = new ScopeSpans();
        for (var index = 0; index < timings.Length; index++)
        {
            var (start, end) = timings[index];
            scopeSpans.Spans.Add(new Span
            {
                TraceId = traceId,
                SpanId = index is 0
                    ? rootSpanId
                    : ByteString.CopyFrom(Enumerable.Repeat((byte)(identity + index), 8).ToArray()),
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
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider()
        };
        context.Request.QueryString = new QueryString(query);
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<CursorPageTrace> ReadPageAsync(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        var page = await JsonSerializer.DeserializeAsync(
            context.Response.Body,
            QylSerializerContext.Default.CursorPageTrace,
            TestContext.Current.CancellationToken);
        return Assert.IsType<CursorPageTrace>(page);
    }
}
