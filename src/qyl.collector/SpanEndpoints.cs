namespace qyl.collector;

/// <summary>
///     Span API endpoints.
/// </summary>
internal static class SpanEndpoints
{
    public static async Task<IResult> GetSessionSpansAsync(string sessionId, DuckDbStore store,
        CancellationToken ct)
    {
        var spans = await store.GetSpansBySessionAsync(sessionId, ct).ConfigureAwait(false);

        // Extract service name from first span's attributes if available
        var serviceName = "unknown";
        if (spans.Count > 0 && spans[0].AttributesJson is { } attrJson)
        {
            try
            {
                var attrs = JsonSerializer.Deserialize(attrJson,
                    QylSerializerContext.Default.DictionaryStringString);
                if (attrs?.TryGetValue("service.name", out var svc) == true)
                    serviceName = svc;
            }
            catch
            {
                /* ignore parse errors */
            }
        }

        var spanDtos = spans.Select(s => SpanMapper.ToDto(s, serviceName)).ToList();
        return Results.Ok(new SpanListResponseDto { Spans = spanDtos });
    }

    public static async Task<IResult> GetTraceAsync(string traceId, DuckDbStore store)
    {
        var spans = await store.GetTraceAsync(traceId).ConfigureAwait(false);
        if (spans.Count is 0) return Results.NotFound();

        var spanDtos = SpanMapper.ToDtos(spans, static r => (r.Name.Split(' ').LastOrDefault() ?? "unknown", null));
        var rootSpan = spanDtos.FirstOrDefault(static s => s.ParentSpanId is null);

        return Results.Ok(new TraceResponseDto
        {
            TraceId = traceId,
            Spans = spanDtos,
            RootSpan = rootSpan,
            DurationMs = rootSpan?.DurationMs,
            Status = rootSpan?.Status
        });
    }
}
