namespace qyl.collector;

/// <summary>
///     Span API endpoints.
/// </summary>
internal static class SpanEndpoints
{
    private static readonly JsonSerializerOptions SnakeCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public static async Task<IResult> GetSessionSpansAsync(string sessionId, DuckDbStore store,
        CancellationToken ct)
    {
        var spans = await store.GetSpansBySessionAsync(sessionId, ct).ConfigureAwait(false);
        return Results.Json(new { items = spans, total = spans.Count }, SnakeCaseOptions);
    }

    public static async Task<IResult> GetTraceSpansAsync(string traceId, DuckDbStore store,
        CancellationToken ct)
    {
        var spans = await store.GetTraceAsync(traceId, ct).ConfigureAwait(false);
        if (spans.Count is 0) return Results.NotFound();
        return Results.Json(new { items = spans, total = spans.Count }, SnakeCaseOptions);
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
