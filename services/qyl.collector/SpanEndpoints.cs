using Qyl.Api.Contracts.Common.Pagination;

namespace Qyl.Collector;

internal static class SpanEndpoints
{
    public static async Task<IResult> GetSessionTracesAsync(
        string sessionId,
        DuckDbStore store,
        CancellationToken ct)
    {
        var spans = await store.GetSpansBySessionAsync(sessionId, ct: ct).ConfigureAwait(false);
        var traces = spans
            .GroupBy(static span => span.TraceId, StringComparer.Ordinal)
            .Select(static group =>
            {
                var spanContracts = SpanMapper.ToContracts(
                    group,
                    static s => (s.ServiceName ?? "unknown", null));
                return SpanMapper.ToTrace(group.Key, spanContracts);
            })
            .ToList();

        return TypedResults.Ok(new CursorPageTrace { Items = traces, HasMore = false });
    }

    public static async Task<IResult> GetTraceSpansAsync(
        string traceId,
        DuckDbStore store,
        CancellationToken ct)
    {
        var spans = await store.GetTraceAsync(traceId, ct: ct).ConfigureAwait(false);
        if (spans.Count is 0) return TypedResults.NotFound(ContractErrorFactory.NotFound("trace", traceId));

        var spanContracts = SpanMapper.ToContracts(
            spans,
            static s => (s.ServiceName ?? "unknown", null));
        return TypedResults.Ok(new CursorPageSpan { Items = spanContracts, HasMore = false });
    }

    public static async Task<IResult> GetTraceAsync(string traceId, DuckDbStore store)
    {
        var spans = await store.GetTraceAsync(traceId).ConfigureAwait(false);
        if (spans.Count is 0) return TypedResults.NotFound(ContractErrorFactory.NotFound("trace", traceId));

        var spanContracts = SpanMapper.ToContracts(
            spans,
            static r => (r.ServiceName ?? "unknown", null));

        return TypedResults.Ok(SpanMapper.ToTrace(traceId, spanContracts));
    }
}
