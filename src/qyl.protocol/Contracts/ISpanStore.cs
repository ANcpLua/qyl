// =============================================================================
// qyl.protocol - ISpanStore Contract
// Interface for span storage operations
// =============================================================================

using qyl.protocol.Models;
using qyl.protocol.Primitives;

namespace qyl.protocol.Contracts;

/// <summary>
///     Contract for span storage and retrieval operations.
/// </summary>
public interface ISpanStore
{
    /// <summary>Stores a batch of spans.</summary>
    /// <param name="spans">The spans to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask StoreAsync(IEnumerable<SpanRecord> spans, CancellationToken cancellationToken = default);

    /// <summary>Gets spans matching the specified criteria.</summary>
    /// <param name="serviceName">Optional service name filter.</param>
    /// <param name="fromTime">Optional start time filter.</param>
    /// <param name="toTime">Optional end time filter.</param>
    /// <param name="genAiOnly">If true, only return GenAI spans.</param>
    /// <param name="limit">Maximum number of spans to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    IAsyncEnumerable<SpanRecord> GetSpansAsync(
        string? serviceName = null,
        UnixNano? fromTime = null,
        UnixNano? toTime = null,
        bool genAiOnly = false,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>Gets a trace by its ID.</summary>
    /// <param name="traceId">The trace ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<TraceNode?> GetTraceAsync(string traceId, CancellationToken cancellationToken = default);

    /// <summary>Gets spans for a specific session.</summary>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    IAsyncEnumerable<SpanRecord> GetSessionSpansAsync(string sessionId, CancellationToken cancellationToken = default);
}
