namespace qyl.collector.Realtime;

/// <summary>
///     REST endpoints for querying spans from in-memory <see cref="SpanRingBuffer"/>.
///     Provides sub-millisecond queries for recent telemetry data.
/// </summary>
public static class SpanMemoryEndpoints
{
    /// <summary>
    ///     Maps the memory-backed span endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapSpanMemoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/spans/recent", GetRecentSpans)
            .WithName("GetRecentSpans")
            .WithDescription("Get recent spans from in-memory buffer (sub-ms latency)");

        endpoints.MapGet("/api/v1/spans/recent/trace/{traceId}", GetTraceFromMemory)
            .WithName("GetTraceFromMemory")
            .WithDescription("Get trace spans from in-memory buffer if available");

        endpoints.MapGet("/api/v1/spans/recent/session/{sessionId}", GetSessionSpansFromMemory)
            .WithName("GetSessionSpansFromMemory")
            .WithDescription("Get session spans from in-memory buffer");

        endpoints.MapGet("/api/v1/spans/buffer/stats", GetBufferStats)
            .WithName("GetBufferStats")
            .WithDescription("Get ring buffer statistics");

        return endpoints;
    }

    [UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
        Justification = "OTLP telemetry contains dynamic user-defined attributes that cannot be statically analyzed")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "OTLP telemetry contains dynamic user-defined attributes that cannot be statically analyzed")]
    private static IResult GetRecentSpans(
        SpanRingBuffer buffer,
        int? limit)
    {
        var spans = buffer.GetLatest(limit ?? 100, out var generation);
        var dtos = SpanMapper.ToDtos(spans, static r => (r.ServiceName ?? "unknown", null));

        return Results.Ok(new RecentSpansResponse
        {
            Spans = dtos,
            Generation = generation,
            Source = "memory",
            BufferCount = buffer.Count,
            BufferCapacity = buffer.Capacity
        });
    }

    [UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
        Justification = "OTLP telemetry contains dynamic user-defined attributes that cannot be statically analyzed")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "OTLP telemetry contains dynamic user-defined attributes that cannot be statically analyzed")]
    private static IResult GetTraceFromMemory(
        string traceId,
        SpanRingBuffer buffer)
    {
        var spans = buffer.GetByTraceId(traceId, out var generation);

        if (spans.Length is 0)
        {
            return Results.Ok(new TraceFromMemoryResponse
            {
                TraceId = traceId,
                Found = false,
                Spans = [],
                Generation = generation,
                Source = "memory"
            });
        }

        var dtos = SpanMapper.ToDtos(spans, static r => (r.ServiceName ?? "unknown", null));
        var rootSpan = dtos.FirstOrDefault(static s => s.ParentSpanId is null);

        return Results.Ok(new TraceFromMemoryResponse
        {
            TraceId = traceId,
            Found = true,
            Spans = dtos,
            RootSpan = rootSpan,
            DurationMs = rootSpan?.DurationMs,
            Generation = generation,
            Source = "memory"
        });
    }

    [UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
        Justification = "OTLP telemetry contains dynamic user-defined attributes that cannot be statically analyzed")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "OTLP telemetry contains dynamic user-defined attributes that cannot be statically analyzed")]
    private static IResult GetSessionSpansFromMemory(
        string sessionId,
        SpanRingBuffer buffer,
        int? limit)
    {
        var spans = buffer.GetBySessionId(sessionId, limit ?? 100, out var generation);
        var dtos = SpanMapper.ToDtos(spans, static r => (r.ServiceName ?? "unknown", null));

        return Results.Ok(new SessionSpansFromMemoryResponse
        {
            SessionId = sessionId,
            Spans = dtos,
            Generation = generation,
            Source = "memory"
        });
    }

    private static IResult GetBufferStats(SpanRingBuffer buffer)
    {
        return Results.Ok(new BufferStatsResponse
        {
            Count = buffer.Count,
            Capacity = buffer.Capacity,
            Generation = buffer.Generation,
            UtilizationPercent = buffer.Capacity > 0
                ? Math.Round(100.0 * buffer.Count / buffer.Capacity, 2, MidpointRounding.ToEven)
                : 0
        });
    }
}

/// <summary>
///     Response for recent spans query.
/// </summary>
public sealed record RecentSpansResponse
{
    public required List<SpanDto> Spans { get; init; }
    public required ulong Generation { get; init; }
    public required string Source { get; init; }
    public required int BufferCount { get; init; }
    public required int BufferCapacity { get; init; }
}

/// <summary>
///     Response for trace from memory query.
/// </summary>
public sealed record TraceFromMemoryResponse
{
    public required string TraceId { get; init; }
    public required bool Found { get; init; }
    public required List<SpanDto> Spans { get; init; }
    public SpanDto? RootSpan { get; init; }
    public double? DurationMs { get; init; }
    public required ulong Generation { get; init; }
    public required string Source { get; init; }
}

/// <summary>
///     Response for session spans from memory query.
/// </summary>
public sealed record SessionSpansFromMemoryResponse
{
    public required string SessionId { get; init; }
    public required List<SpanDto> Spans { get; init; }
    public required ulong Generation { get; init; }
    public required string Source { get; init; }
}

/// <summary>
///     Response for buffer statistics.
/// </summary>
public sealed record BufferStatsResponse
{
    public required int Count { get; init; }
    public required int Capacity { get; init; }
    public required ulong Generation { get; init; }
    public required double UtilizationPercent { get; init; }
}
