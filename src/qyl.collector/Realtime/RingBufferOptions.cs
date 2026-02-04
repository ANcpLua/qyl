namespace qyl.collector.Realtime;

/// <summary>
///     Configuration for <see cref="SpanRingBuffer" /> and related services.
/// </summary>
public sealed class RingBufferOptions
{
    /// <summary>
    ///     Number of spans to keep in memory. Default: 10,000.
    /// </summary>
    public int Capacity { get; set; } = 10_000;

    /// <summary>
    ///     Interval between DuckDB flush batches. Default: 5 seconds.
    /// </summary>
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     Maximum spans per flush batch. Default: 1,000.
    /// </summary>
    public int FlushBatchSize { get; set; } = 1_000;

    /// <summary>
    ///     Interval for SSE generation polling. Default: 100ms.
    /// </summary>
    public TimeSpan SsePollInterval { get; set; } = TimeSpan.FromMilliseconds(100);
}
