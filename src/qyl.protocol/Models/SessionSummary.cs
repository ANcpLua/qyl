// =============================================================================
// qyl.protocol - SessionSummary Model
// Aggregated session information
// =============================================================================

using qyl.protocol.Primitives;

namespace qyl.protocol.Models;

/// <summary>
///     Summary of a session with aggregated metrics.
/// </summary>
public sealed record SessionSummary
{
    /// <summary>Session identifier.</summary>
    public required string SessionId { get; init; }

    /// <summary>Primary service name for this session.</summary>
    public string? ServiceName { get; init; }

    /// <summary>Session start time.</summary>
    public UnixNano StartTime { get; init; }

    /// <summary>Session end time (last span).</summary>
    public UnixNano EndTime { get; init; }

    /// <summary>Total number of spans in the session.</summary>
    public int SpanCount { get; init; }

    /// <summary>Number of GenAI spans in the session.</summary>
    public int GenAiSpanCount { get; init; }

    /// <summary>Total input tokens across all GenAI spans.</summary>
    public long TotalInputTokens { get; init; }

    /// <summary>Total output tokens across all GenAI spans.</summary>
    public long TotalOutputTokens { get; init; }

    /// <summary>Total tokens (input + output).</summary>
    public long TotalTokens => TotalInputTokens + TotalOutputTokens;

    /// <summary>Number of distinct trace IDs in the session.</summary>
    public int TraceCount { get; init; }

    /// <summary>Number of error spans in the session.</summary>
    public int ErrorCount { get; init; }

    /// <summary>Primary GenAI provider used in the session.</summary>
    public string? PrimaryProvider { get; init; }

    /// <summary>Primary model used in the session.</summary>
    public string? PrimaryModel { get; init; }

    /// <summary>Total duration of the session in nanoseconds.</summary>
    public long DurationNs => EndTime.Value - StartTime.Value;

    /// <summary>Whether the session has any errors.</summary>
    public bool HasErrors => ErrorCount > 0;
}
