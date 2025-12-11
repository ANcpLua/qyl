using System.Collections.Concurrent;
using System.Threading.Channels;
using qyl.grpc.Extraction;
using qyl.grpc.Models;

namespace qyl.grpc.Aggregation;

public sealed class SessionAggregator : ISessionAggregator,
    IDisposable
{
    private readonly PriorityQueue<string, long> _evictionQueue = new();

    private readonly Channel<EnrichedSpan> _ingestChannel;

    private readonly int _maxSessions;
    private readonly Task _processingTask;

    private readonly Lock _queueLock = new();

    private readonly ConcurrentDictionary<string, SessionBuilder> _sessions = new(StringComparer.Ordinal);
    private readonly TimeSpan _sessionTimeout;
    private readonly CancellationTokenSource _shutdownCts = new();
    private long _globalCostMicros;
    private long _globalErrors;
    private long _globalInputTokens;
    private long _globalOutputTokens;

    private long _globalSpans;
    private long _globalToolCalls;

    public SessionAggregator(int maxSessions = 10_000, TimeSpan? sessionTimeout = null)
    {
        _maxSessions = maxSessions;
        _sessionTimeout = sessionTimeout ?? TimeSpan.FromHours(24);

        _ingestChannel = Channel.CreateUnbounded<EnrichedSpan>(
            new()
            {
                SingleReader = true,
                SingleWriter = false
            });

        _processingTask = Task.Run(ProcessLoopAsync, _shutdownCts.Token);
    }

    public void Dispose()
    {
        _shutdownCts.Cancel();
#pragma warning disable MA0040 // Forward the CancellationToken - token is cancelled, Wait() is intentional for cleanup
        _processingTask.Wait();
#pragma warning restore MA0040
        _shutdownCts.Dispose();
    }

    public void AddSpan(SpanModel span) =>
        AddSpan();

    public void AddSpan(EnrichedSpan enriched) =>
        _ingestChannel.Writer.TryWrite(enriched);

    public SessionModel? GetSession(string sessionId) =>
        _sessions.TryGetValue(sessionId, out var builder) ? builder.Build() : null;

    public IReadOnlyList<SessionModel> Query(SessionQuery query)
    {
        var builders = _sessions.Values.AsEnumerable();

        if (query.ServiceName is not null)
            builders = builders.Where(b => b.ServiceName == query.ServiceName);

        if (query.From.HasValue)
            builders = builders.Where(b => b.LastActivity >= query.From.Value);

        if (query.MinTokens.HasValue)
            builders = builders.Where(b => b.LiveTotalTokens >= query.MinTokens.Value);

        if (query.HasErrors == true)
            builders = builders.Where(b => b.HasErrors);

        return
        [
            .. builders
                .OrderByDescending(b => b.LastActivity)
                .Skip(query.Offset)
                .Take(query.Limit)
                .Select(b => b.Build())
        ];
    }

    public SessionStatistics GetStatistics()
    {
        var builderSnapshot = _sessions.Values.ToList();

        var topModels = builderSnapshot
            .Select(b => b.PrimaryModel)
            .Where(m => m is not null)
            .CountBy(m => m!)
            .OrderByDescending(kv => kv.Value)
            .Take(5)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(5);

        return new(
            _sessions.Count,
            builderSnapshot.Count(b => b.LastActivity > cutoff),
            Interlocked.Read(ref _globalSpans),
            Interlocked.Read(ref _globalInputTokens),
            Interlocked.Read(ref _globalOutputTokens),
            Interlocked.Read(ref _globalCostMicros) / 1_000_000m,
            (int)Interlocked.Read(ref _globalToolCalls),
            (int)Interlocked.Read(ref _globalErrors),
            topModels
        );
    }

    public long SessionCount => _sessions.Count;

    public IReadOnlyList<SessionModel> GetRecentSessions(int limit = 100) =>
        Query(new()
        {
            Limit = limit
        });

    public void AddSpan(params ReadOnlySpan<SpanModel> spans)
    {
        foreach (var span in spans)
        {
            var enriched = GenAiExtractor.Enrich(span);
            _ingestChannel.Writer.TryWrite(enriched);
        }
    }

    private async Task ProcessLoopAsync()
    {
        try
        {
            while (await _ingestChannel.Reader.WaitToReadAsync(_shutdownCts.Token))
            {
                while (_ingestChannel.Reader.TryRead(out var span)) MergeSpanInternal(span);

                EvictExpiredSessions();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void MergeSpanInternal(EnrichedSpan span)
    {
        var sessionId = span.SessionId ?? span.TraceId;
        var builder = _sessions.GetOrAdd(sessionId, id => new(id, span.ServiceName));

        builder.AddSpan(span);

        Interlocked.Increment(ref _globalSpans);
        if (span.IsGenAiSpan)
        {
            Interlocked.Add(ref _globalInputTokens, span.GenAiInputTokens);
            Interlocked.Add(ref _globalOutputTokens, span.GenAiOutputTokens);
            if (span.GenAi?.IsToolCall == true) Interlocked.Increment(ref _globalToolCalls);

            var costMicros = (long)(span.GenAiCostUsd * 1_000_000m);
            Interlocked.Add(ref _globalCostMicros, costMicros);
        }

        if (span.Status == SpanStatus.Error) Interlocked.Increment(ref _globalErrors);

        lock (_queueLock)
        {
            _evictionQueue.Enqueue(sessionId, builder.LastActivity.Ticks);
        }
    }

    private void EvictExpiredSessions()
    {
        lock (_queueLock)
        {
            if (_sessions.Count <= _maxSessions && _evictionQueue.Count == 0) return;

            var cutoffTicks = (DateTimeOffset.UtcNow - _sessionTimeout).Ticks;

            while (_evictionQueue.TryPeek(out var id, out var priorityTicks))
            {
                if (priorityTicks > cutoffTicks && _sessions.Count <= _maxSessions) break;

                _evictionQueue.Dequeue();

                if (_sessions.TryGetValue(id, out var builder))
                {
                    if (builder.LastActivity.Ticks <= priorityTicks)

                        _sessions.TryRemove(id, out _);
                    else if (_sessions.Count > _maxSessions)

                        _sessions.TryRemove(id, out _);
                }
            }
        }
    }

    public SessionModel? GetSession(ReadOnlySpan<char> sessionId)
    {
        var lookup = _sessions.GetAlternateLookup<ReadOnlySpan<char>>();
        return lookup.TryGetValue(sessionId, out var builder) ? builder.Build() : null;
    }

    private sealed class SessionBuilder
    {
        private readonly Lock _lock = new();

        private readonly Dictionary<string, int> _modelCounts = [];
        private readonly string _sessionId;
        private readonly List<EnrichedSpan> _spans = [];
        private readonly HashSet<string> _traceIds = [];

        public SessionBuilder(string sessionId, string serviceName)
        {
            _sessionId = sessionId;
            ServiceName = serviceName;
            StartTime = DateTimeOffset.UtcNow;
            LastActivity = DateTimeOffset.UtcNow;
        }

        public DateTimeOffset LastActivity { get; private set; }
        public DateTimeOffset StartTime { get; private set; }
        public long LiveTotalTokens { get; private set; }
        public bool HasErrors { get; private set; }
        public string? PrimaryModel { get; private set; }
        public string ServiceName { get; }

        public void AddSpan(EnrichedSpan span)
        {
            using (_lock.EnterScope())
            {
                if (_spans.Count == 0) StartTime = span.StartTime;

                _spans.Add(span);
                _traceIds.Add(span.TraceId);
                LastActivity = DateTimeOffset.UtcNow;

                if (span.Status == SpanStatus.Error) HasErrors = true;

                if (span.IsGenAiSpan)
                {
                    LiveTotalTokens += span.GenAiInputTokens + span.GenAiOutputTokens;

                    if (span.GenAiModel is not null)
                    {
                        var count = _modelCounts.GetValueOrDefault(span.GenAiModel, 0);
                        _modelCounts[span.GenAiModel] = count + 1;

                        if (PrimaryModel == null || _modelCounts[span.GenAiModel] > _modelCounts[PrimaryModel])
                            PrimaryModel = span.GenAiModel;
                    }
                }
            }
        }

        public SessionModel Build()
        {
            using (_lock.EnterScope())
            {
                var spans = _spans.OrderBy(s => s.StartTime).ToList();
                var genAiSpans = spans.Where(s => s.IsGenAiSpan).ToList();

                return new(
                    _sessionId,
                    ServiceName,
                    spans,
                    [.. _traceIds],
                    StartTime,
                    LastActivity,
                    spans.Count,
                    spans.Count(s => s.Status == SpanStatus.Error),
                    spans.Count > 0
                        ? (double)spans.Count(s => s.Status == SpanStatus.Error) / spans.Count
                        : 0,
                    genAiSpans.Sum(s => s.GenAiInputTokens),
                    genAiSpans.Sum(s => s.GenAiOutputTokens),
                    LiveTotalTokens,
                    genAiSpans.Sum(s => s.GenAiCostUsd),
                    genAiSpans.Count(s => s.GenAi?.IsToolCall == true),
                    PrimaryModel,
                    [.. _modelCounts.Keys]);
            }
        }
    }
}

public interface ISessionAggregator
{
    long SessionCount { get; }
    void AddSpan(SpanModel span);
    void AddSpan(EnrichedSpan enriched);
    SessionModel? GetSession(string sessionId);
    IReadOnlyList<SessionModel> GetRecentSessions(int limit = 100);
    IReadOnlyList<SessionModel> Query(SessionQuery query);
    SessionStatistics GetStatistics();
}

public sealed record SessionModel(
    string SessionId,
    string ServiceName,
    IReadOnlyList<EnrichedSpan> Spans,
    IReadOnlyList<string> TraceIds,
    DateTimeOffset StartTime,
    DateTimeOffset LastActivity,
    int SpanCount,
    int ErrorCount,
    double ErrorRate,
    long TotalInputTokens,
    long TotalOutputTokens,
    long TotalTokens,
    decimal TotalCostUsd,
    int ToolCallCount,
    string? PrimaryModel,
    IReadOnlyList<string> Models)
{
    public TimeSpan Duration => LastActivity - StartTime;
    public double DurationMinutes => Duration.TotalMinutes;
    public bool HasErrors => ErrorCount > 0;
    public bool IsActive => LastActivity > DateTimeOffset.UtcNow - TimeSpan.FromMinutes(5);
}

public sealed record SessionQuery(
    string? ServiceName = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    long? MinTokens = null,
    bool? HasErrors = null,
    int Limit = 100,
    int Offset = 0);

public sealed record SessionStatistics(
    int TotalSessions,
    int ActiveSessions,
    long TotalSpans,
    long TotalInputTokens,
    long TotalOutputTokens,
    decimal TotalCostUsd,
    int TotalToolCalls,
    int TotalErrors,
    IReadOnlyDictionary<string, int> TopModels);
