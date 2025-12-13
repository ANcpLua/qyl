// =============================================================================
// qyl.protocol - ISessionAggregator Contract
// Interface for session aggregation operations
// =============================================================================

using Qyl.Protocol.Models;

namespace Qyl.Protocol.Contracts;

/// <summary>
///     Contract for session aggregation and summary operations.
/// </summary>
public interface ISessionAggregator
{
    /// <summary>Gets session summaries ordered by most recent.</summary>
    /// <param name="limit">Maximum number of sessions to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    IAsyncEnumerable<SessionSummary> GetSessionsAsync(int limit = 50, CancellationToken cancellationToken = default);

    /// <summary>Gets a specific session summary by ID.</summary>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<SessionSummary?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>Gets sessions for a specific service.</summary>
    /// <param name="serviceName">The service name.</param>
    /// <param name="limit">Maximum number of sessions to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    IAsyncEnumerable<SessionSummary> GetSessionsByServiceAsync(
        string serviceName,
        int limit = 50,
        CancellationToken cancellationToken = default);

    /// <summary>Gets sessions with GenAI activity.</summary>
    /// <param name="limit">Maximum number of sessions to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    IAsyncEnumerable<SessionSummary> GetGenAiSessionsAsync(
        int limit = 50,
        CancellationToken cancellationToken = default);
}