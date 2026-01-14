// =============================================================================
// qyl Ingestion Context - Request-scoped state for async boundaries
// Uses Microsoft.Extensions.AsyncState for IAsyncContext<T>
// =============================================================================

namespace qyl.collector.Telemetry;

/// <summary>
///     Request-scoped context that flows across async boundaries.
///     Used by log enrichers to add consistent tags to all logs in a request.
/// </summary>
public sealed class IngestionContext
{
    /// <summary>Session ID being processed.</summary>
    public string? SessionId { get; set; }

    /// <summary>Service name from the telemetry data.</summary>
    public string? ServiceName { get; set; }

    /// <summary>GenAI provider (openai, anthropic, etc.).</summary>
    public string? Provider { get; set; }

    /// <summary>Model being used.</summary>
    public string? Model { get; set; }

    /// <summary>Trace ID for correlation.</summary>
    public string? TraceId { get; set; }

    /// <summary>Number of spans in the batch.</summary>
    public int SpanCount { get; set; }
}
