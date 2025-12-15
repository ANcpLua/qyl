namespace qyl.collector.Storage;

public sealed record StorageStats
{
    public long SpanCount { get; init; }
    public long SessionCount { get; init; }
    public long FeedbackCount { get; init; }
    public DateTime? OldestSpan { get; init; }
    public DateTime? NewestSpan { get; init; }
}
