using Qyl.Api.Contracts.Common.Pagination;

namespace Qyl.Collector;

internal static class SpanEndpoints
{
    public static async Task<IResult> GetSessionSpansAsync(string sessionId, DuckDbStore store,
        CancellationToken ct)
    {
        var spans = await store.GetSpansBySessionAsync(sessionId, ct).ConfigureAwait(false);

        var serviceName = "unknown";
        if (spans.Count > 0 && spans[0].AttributesJson is { } attrJson)
        {
            try
            {
                var attrs = JsonSerializer.Deserialize(attrJson,
                    QylSerializerContext.Default.DictionaryStringString);
                if (attrs?.TryGetValue(SemanticAttributeKeys.ServiceName, out var svc) == true)
                    serviceName = svc;
            }
            catch (JsonException ex)
            {
                Debug.WriteLine(ex);
            }
        }

        var spanContracts = spans.Select(s => SpanMapper.ToContract(s, serviceName)).ToList();
        return TypedResults.Ok(new CursorPageSpan { Items = spanContracts, HasMore = false });
    }

    public static async Task<IResult> GetTraceAsync(string traceId, DuckDbStore store)
    {
        var spans = await store.GetTraceAsync(traceId).ConfigureAwait(false);
        if (spans.Count is 0) return TypedResults.NotFound();

        var spanContracts = SpanMapper.ToContracts(
            spans,
            static r => (r.ServiceName ?? r.Name.Split(' ').LastOrDefault() ?? "unknown", null));

        return TypedResults.Ok(SpanMapper.ToTrace(traceId, spanContracts));
    }
}
