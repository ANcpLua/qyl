// =============================================================================
// qyl.protocol - SpanRecord Model
// Core span representation used across all qyl components
// =============================================================================

using qyl.protocol.Primitives;

namespace qyl.protocol.Models;

/// <summary>
///     Represents a span record stored in DuckDB and transmitted via API.
/// </summary>
public sealed record SpanRecord
{
    /// <summary>Trace ID (32 hex chars).</summary>
    public required string TraceId { get; init; }

    /// <summary>Span ID (16 hex chars).</summary>
    public required string SpanId { get; init; }

    /// <summary>Parent span ID (null for root spans).</summary>
    public string? ParentSpanId { get; init; }

    /// <summary>Session ID for grouping related traces.</summary>
    public string? SessionId { get; init; }

    /// <summary>Span name/operation.</summary>
    public required string Name { get; init; }

    /// <summary>Service name from resource attributes.</summary>
    public string? ServiceName { get; init; }

    /// <summary>Span kind (0=Unspecified, 1=Internal, 2=Server, 3=Client, 4=Producer, 5=Consumer).</summary>
    public int Kind { get; init; }

    /// <summary>Start time in Unix nanoseconds.</summary>
    public UnixNano StartTimeUnixNano { get; init; }

    /// <summary>End time in Unix nanoseconds.</summary>
    public UnixNano EndTimeUnixNano { get; init; }

    /// <summary>Duration in nanoseconds (computed).</summary>
    public long DurationNs => EndTimeUnixNano.Value - StartTimeUnixNano.Value;

    /// <summary>Status code (0=Unset, 1=Ok, 2=Error).</summary>
    public int StatusCode { get; init; }

    /// <summary>Status message (for errors).</summary>
    public string? StatusMessage { get; init; }

    /// <summary>GenAI-specific data (null if not a GenAI span).</summary>
    public GenAiSpanData? GenAi { get; init; }

    /// <summary>Additional attributes as key-value pairs.</summary>
    public IReadOnlyDictionary<string, string>? Attributes { get; init; }

    /// <summary>Span events as JSON.</summary>
    public string? EventsJson { get; init; }

    /// <summary>Span links as JSON.</summary>
    public string? LinksJson { get; init; }
}