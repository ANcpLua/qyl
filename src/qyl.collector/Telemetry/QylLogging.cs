// =============================================================================
// qyl Source-Generated Logging - .NET 10 LoggerMessage Patterns
// Uses [TagName] for OTel-compatible logging
// =============================================================================

namespace qyl.collector.Telemetry;

// =============================================================================
// Source-Generated Log Methods
// =============================================================================

/// <summary>
///     High-performance source-generated logging for qyl.collector.
///     Uses .NET 8+ [TagName] attributes for OTel semantic convention compliance.
/// </summary>
public static partial class Log
{
    // =========================================================================
    // Startup/Shutdown
    // =========================================================================

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "qyl.collector starting on port {Port} (gRPC: {GrpcPort})")]
    public static partial void Starting(
        ILogger logger,
        [TagName("server.port")] int port,
        [TagName("grpc.port")] int grpcPort);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "qyl.collector started successfully")]
    public static partial void Started(ILogger logger);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "qyl.collector shutting down")]
    public static partial void ShuttingDown(ILogger logger);

    // =========================================================================
    // Ingestion
    // =========================================================================

    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Debug,
        Message = "Ingesting {SpanCount} spans")]
    public static partial void IngestingSpans(
        ILogger logger,
        [TagName("span.count")] int spanCount);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Ingested batch: {SpanCount} spans ({GenAiSpanCount} GenAI) in {DurationMs:F1}ms")]
    public static partial void IngestedBatch(
        ILogger logger,
        [TagName("span.count")] int spanCount,
        [TagName("genai.span.count")] int genAiSpanCount,
        [TagName("duration_ms")] double durationMs,
        [TagName("gen_ai.provider.name")] string? provider = null,
        [TagName("gen_ai.request.model")] string? model = null);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Warning,
        Message = "Ingestion batch empty - no spans to process")]
    public static partial void EmptyBatch(ILogger logger);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Error,
        Message = "Ingestion failed: {ErrorType}")]
    public static partial void IngestionFailed(
        ILogger logger,
        [TagName("error.type")] string errorType,
        [TagName("gen_ai.provider.name")] string? provider,
        Exception ex);

    // =========================================================================
    // GenAI Processing
    // =========================================================================

    [LoggerMessage(
        EventId = 3000,
        Level = LogLevel.Debug,
        Message = "Processing GenAI span: {Provider}/{Model}")]
    public static partial void ProcessingGenAiSpan(
        ILogger logger,
        [TagName("gen_ai.provider.name")] string? provider,
        [TagName("gen_ai.request.model")] string? model,
        [TagName("gen_ai.operation.name")] string? operation = null,
        [TagName("session.id")] string? sessionId = null);

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Information,
        Message = "GenAI span processed: {InputTokens} in, {OutputTokens} out")]
    public static partial void GenAiSpanProcessed(
        ILogger logger,
        [TagName("gen_ai.usage.input_tokens")] long inputTokens,
        [TagName("gen_ai.usage.output_tokens")]
        long outputTokens,
        [TagName("gen_ai.provider.name")] string? provider = null,
        [TagName("gen_ai.request.model")] string? model = null);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Debug,
        Message = "Normalized deprecated attribute: {OldName} -> {NewName}")]
    public static partial void NormalizedAttribute(
        ILogger logger,
        string oldName,
        string newName);

    // =========================================================================
    // Storage
    // =========================================================================

    [LoggerMessage(
        EventId = 4000,
        Level = LogLevel.Debug,
        Message = "Storing {SpanCount} spans to DuckDB")]
    public static partial void StoringSpans(
        ILogger logger,
        [TagName("span.count")] int spanCount);

    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Information,
        Message = "Stored {SpanCount} spans in {DurationMs:F1}ms")]
    public static partial void StoredSpans(
        ILogger logger,
        [TagName("span.count")] int spanCount,
        [TagName("duration_ms")] double durationMs);

    [LoggerMessage(
        EventId = 4002,
        Level = LogLevel.Error,
        Message = "Storage operation failed: {ErrorType}")]
    public static partial void StorageFailed(
        ILogger logger,
        [TagName("error.type")] string errorType,
        [TagName("session.id")] string? sessionId,
        Exception ex);

    // =========================================================================
    // Queries
    // =========================================================================

    [LoggerMessage(
        EventId = 5000,
        Level = LogLevel.Debug,
        Message = "Querying session {SessionId}")]
    public static partial void QueryingSession(
        ILogger logger,
        [TagName("session.id")] string sessionId);

    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Information,
        Message = "Session query completed: {SpanCount} spans in {DurationMs:F1}ms")]
    public static partial void SessionQueryCompleted(
        ILogger logger,
        [TagName("session.id")] string sessionId,
        [TagName("span.count")] int spanCount,
        [TagName("duration_ms")] double durationMs);

    [LoggerMessage(
        EventId = 5002,
        Level = LogLevel.Warning,
        Message = "Session not found: {SessionId}")]
    public static partial void SessionNotFound(
        ILogger logger,
        [TagName("session.id")] string sessionId);

    // =========================================================================
    // SSE/Streaming
    // =========================================================================

    [LoggerMessage(
        EventId = 6000,
        Level = LogLevel.Debug,
        Message = "SSE client connected: {ClientId}")]
    public static partial void SseClientConnected(
        ILogger logger,
        [TagName("client.id")] string clientId);

    [LoggerMessage(
        EventId = 6001,
        Level = LogLevel.Debug,
        Message = "SSE client disconnected: {ClientId}")]
    public static partial void SseClientDisconnected(
        ILogger logger,
        [TagName("client.id")] string clientId);

    [LoggerMessage(
        EventId = 6002,
        Level = LogLevel.Debug,
        Message = "Broadcasting {SpanCount} spans to {ClientCount} clients")]
    public static partial void BroadcastingSpans(
        ILogger logger,
        [TagName("span.count")] int spanCount,
        [TagName("client.count")] int clientCount);

    // =========================================================================
    // Authentication
    // =========================================================================

    [LoggerMessage(
        EventId = 7000,
        Level = LogLevel.Information,
        Message = "Authentication successful")]
    public static partial void AuthSuccess(ILogger logger);

    [LoggerMessage(
        EventId = 7001,
        Level = LogLevel.Warning,
        Message = "Authentication failed: {Reason}")]
    public static partial void AuthFailed(
        ILogger logger,
        [TagName("auth.failure_reason")] string reason);

    // =========================================================================
    // Health/Diagnostics
    // =========================================================================

    [LoggerMessage(
        EventId = 8000,
        Level = LogLevel.Debug,
        Message = "Health check: {Status}")]
    public static partial void HealthCheck(
        ILogger logger,
        [TagName("health.status")] string status);

    [LoggerMessage(
        EventId = 8001,
        Level = LogLevel.Warning,
        Message = "Resource pressure detected: {Resource} at {UsagePercent:F1}%")]
    public static partial void ResourcePressure(
        ILogger logger,
        string resource,
        double usagePercent);
}
