using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace qyl.copilot.Utils;

/// <summary>
///     Extension methods for converting <see cref="IAsyncEnumerable{T}" /> to SSE results
///     using <see cref="TypedResults" /> Server-Sent Events support (.NET 10).
/// </summary>
public static class SseResultExtensions
{
    /// <summary>
    ///     Wraps the async sequence as a Server-Sent Events HTTP result.
    ///     Non-string payloads are serialized using the configured JSON options.
    /// </summary>
    public static ServerSentEventsResult<T> AsServerSentEvents<T>(
        this IAsyncEnumerable<T> source,
        string? eventType = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        return TypedResults.ServerSentEvents(source, eventType);
    }
}
