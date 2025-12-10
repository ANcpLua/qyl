// =============================================================================
// INGESTION ENDPOINT FIX
// 
// Replace the stub endpoints in Program.cs with these working implementations.
// This wires up: Ingestion → DuckDB Storage + Session Aggregation + SSE Broadcast
// =============================================================================

// Add this using at the top of Program.cs:
// using qyl.collector.Storage;

// Replace lines 166-167 in Program.cs with:

// ============================================================================
// INGESTION API - Actually processes incoming telemetry
// ============================================================================

app.MapPost("/api/v1/ingest", async (
    HttpContext context,
    DuckDbStore store,
    SessionAggregator aggregator,
    ITelemetrySseBroadcaster broadcaster) =>
{
    // Parse incoming spans (qyl native format)
    SpanBatch? batch;
    try
    {
        batch = await context.Request.ReadFromJsonAsync<SpanBatch>(
            QylSerializerContext.Default.SpanBatch);
        
        if (batch is null || batch.Spans.Count == 0)
        {
            return Results.BadRequest(new ErrorResponse("Empty or invalid batch"));
        }
    }
    catch (JsonException ex)
    {
        return Results.BadRequest(new ErrorResponse("Invalid JSON", ex.Message));
    }

    // 1. Store to DuckDB (async, non-blocking)
    await store.EnqueueAsync(batch);

    // 2. Track for session aggregation (in-memory)
    foreach (var span in batch.Spans)
    {
        aggregator.TrackSpan(span);
    }

    // 3. Broadcast to SSE clients
    // Convert SpanRecord to SpanDto for broadcast
    var spanDtos = batch.Spans.Select(s => SpanMapper.ToDto(s, 
        GetServiceNameFromAttributes(s.Attributes) ?? "unknown")).ToList();
    
    await broadcaster.BroadcastSpansAsync(new SpanBatchDto { Spans = spanDtos });

    return Results.Accepted();
});

// OTLP compatibility shim - converts OTLP format to internal format
app.MapPost("/v1/traces", async (
    HttpContext context,
    DuckDbStore store,
    SessionAggregator aggregator,
    ITelemetrySseBroadcaster broadcaster) =>
{
    // TODO: Parse OTLP protobuf format
    // For now, try JSON (OTLP/HTTP JSON format)
    try
    {
        // OTLP has a different structure - need to convert
        var otlpData = await context.Request.ReadFromJsonAsync<OtlpExportTraceServiceRequest>();
        
        if (otlpData?.ResourceSpans is null)
        {
            return Results.BadRequest(new ErrorResponse("Invalid OTLP format"));
        }

        var spans = ConvertOtlpToSpanRecords(otlpData);
        if (spans.Count == 0)
        {
            return Results.Accepted(); // Nothing to process
        }

        var batch = new SpanBatch(spans);
        
        await store.EnqueueAsync(batch);
        
        foreach (var span in spans)
        {
            aggregator.TrackSpan(span);
        }

        var spanDtos = spans.Select(s => SpanMapper.ToDto(s,
            GetServiceNameFromAttributes(s.Attributes) ?? "unknown")).ToList();
        
        await broadcaster.BroadcastSpansAsync(new SpanBatchDto { Spans = spanDtos });

        return Results.Accepted();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new ErrorResponse("OTLP parse error", ex.Message));
    }
});

// =============================================================================
// HELPER METHODS - Add these at the bottom of Program.cs
// =============================================================================

static string? GetServiceNameFromAttributes(string? attributesJson)
{
    if (string.IsNullOrEmpty(attributesJson)) return null;
    
    try
    {
        using var doc = JsonDocument.Parse(attributesJson);
        if (doc.RootElement.TryGetProperty("service.name", out var svc) &&
            svc.ValueKind == JsonValueKind.String)
        {
            return svc.GetString();
        }
    }
    catch { }
    
    return null;
}

// OTLP conversion (simplified - full implementation needs protobuf)
static List<SpanRecord> ConvertOtlpToSpanRecords(OtlpExportTraceServiceRequest otlp)
{
    var spans = new List<SpanRecord>();
    
    foreach (var resourceSpan in otlp.ResourceSpans ?? [])
    {
        var serviceName = resourceSpan.Resource?.Attributes?
            .FirstOrDefault(a => a.Key == "service.name")?.Value?.StringValue ?? "unknown";
        
        foreach (var scopeSpan in resourceSpan.ScopeSpans ?? [])
        {
            foreach (var span in scopeSpan.Spans ?? [])
            {
                spans.Add(new SpanRecord
                {
                    TraceId = span.TraceId ?? "",
                    SpanId = span.SpanId ?? "",
                    ParentSpanId = span.ParentSpanId,
                    SessionId = null, // Extract from attributes if present
                    Name = span.Name ?? "unknown",
                    Kind = span.Kind?.ToString(),
                    StartTime = DateTimeOffset.FromUnixTimeNanoseconds(span.StartTimeUnixNano).UtcDateTime,
                    EndTime = DateTimeOffset.FromUnixTimeNanoseconds(span.EndTimeUnixNano).UtcDateTime,
                    StatusCode = (int?)span.Status?.Code,
                    StatusMessage = span.Status?.Message,
                    Attributes = JsonSerializer.Serialize(
                        span.Attributes?.ToDictionary(a => a.Key, a => a.Value?.StringValue ?? a.Value?.IntValue?.ToString() ?? ""),
                        QylSerializerContext.Default.DictionaryStringString),
                    Events = null, // TODO: Convert span events
                    // GenAI fields would be extracted from attributes
                    ProviderName = null,
                    RequestModel = null,
                    TokensIn = null,
                    TokensOut = null,
                    CostUsd = null
                });
            }
        }
    }
    
    return spans;
}

// =============================================================================
// OTLP TYPES - Add these records for OTLP JSON parsing
// =============================================================================

// These are simplified OTLP types for JSON parsing
// For full OTLP support, use the official protobuf definitions

public sealed record OtlpExportTraceServiceRequest
{
    public List<OtlpResourceSpans>? ResourceSpans { get; init; }
}

public sealed record OtlpResourceSpans
{
    public OtlpResource? Resource { get; init; }
    public List<OtlpScopeSpans>? ScopeSpans { get; init; }
}

public sealed record OtlpResource
{
    public List<OtlpKeyValue>? Attributes { get; init; }
}

public sealed record OtlpScopeSpans
{
    public List<OtlpSpan>? Spans { get; init; }
}

public sealed record OtlpSpan
{
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
    public string? ParentSpanId { get; init; }
    public string? Name { get; init; }
    public int? Kind { get; init; }
    public long StartTimeUnixNano { get; init; }
    public long EndTimeUnixNano { get; init; }
    public OtlpStatus? Status { get; init; }
    public List<OtlpKeyValue>? Attributes { get; init; }
}

public sealed record OtlpStatus
{
    public int? Code { get; init; }
    public string? Message { get; init; }
}

public sealed record OtlpKeyValue
{
    public string? Key { get; init; }
    public OtlpAnyValue? Value { get; init; }
}

public sealed record OtlpAnyValue
{
    public string? StringValue { get; init; }
    public long? IntValue { get; init; }
    public double? DoubleValue { get; init; }
    public bool? BoolValue { get; init; }
}

// =============================================================================
// SERIALIZER CONTEXT ADDITIONS
// Add these to QylSerializerContext.cs
// =============================================================================

// [JsonSerializable(typeof(OtlpExportTraceServiceRequest))]
// [JsonSerializable(typeof(Dictionary<string, string>), TypeInfoPropertyName = "DictionaryStringString")]
