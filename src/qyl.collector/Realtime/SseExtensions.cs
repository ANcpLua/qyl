// =============================================================================
// SSE Extensions - Graceful error handling wrapper for TypedResults.ServerSentEvents
// Sends error event before closing connection on exceptions
// =============================================================================

namespace qyl.collector.Realtime;

/// <summary>
///     Extensions for graceful SSE error handling.
///     TypedResults.ServerSentEvents does not handle exceptions gracefully -
///     this wrapper adds try-catch with error event emission.
/// </summary>
public static partial class SseExtensions
{
    [LoggerMessage(
        EventId = 6010,
        Level = LogLevel.Error,
        Message = "SSE stream error")]
    private static partial void LogSseStreamError(ILogger logger, Exception ex);

    /// <summary>
    ///     Wraps an IAsyncEnumerable to catch exceptions and emit an error event before closing.
    /// </summary>
    public static async IAsyncEnumerable<SseItem<T>> WithGracefulErrorHandling<T>(
        this IAsyncEnumerable<SseItem<T>> source,
        ILogger? logger = null,
        Func<Exception, T>? errorFactory = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var enumerator = source.GetAsyncEnumerator(ct);
        Exception? caughtException = null;
        try
        {
            while (true)
            {
                SseItem<T> current;
                try
                {
                    if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                        yield break;

                    current = enumerator.Current;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    // Normal cancellation - don't emit error
                    yield break;
                }
                catch (Exception ex)
                {
                    if (logger is not null) LogSseStreamError(logger, ex);
                    caughtException = ex;
                    break; // Exit loop to yield error outside catch
                }

                yield return current;
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }

        // Emit error event outside the catch clause
        if (caughtException is not null && errorFactory is not null)
        {
            yield return new SseItem<T>(errorFactory(caughtException), "error");
        }
    }

    /// <summary>
    ///     Creates an error event DTO for SSE error handling.
    /// </summary>
    public static TelemetryEventDto CreateErrorEvent(Exception ex) =>
        new("error", new SseErrorData(ex.GetType().Name, ex.Message), TimeProvider.System.GetUtcNow());
}

/// <summary>
///     Error data for SSE error events.
/// </summary>
public sealed record SseErrorData(string ErrorType, string Message);
