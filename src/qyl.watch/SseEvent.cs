namespace qyl.watch;

/// <summary>
///     Parsed SSE event from the collector's live stream.
/// </summary>
internal sealed record SseEvent(string Type, string Data);
