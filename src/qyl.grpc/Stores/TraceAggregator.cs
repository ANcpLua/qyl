using System.Collections.Concurrent;
using qyl.grpc.Abstractions;
using qyl.grpc.Models;

namespace qyl.grpc.Stores;

public sealed class TraceAggregator(int maxTraces = 1_000, TimeSpan? traceTimeout = null) : ITraceAggregator
{
    private readonly ConcurrentDictionary<string, TraceBuilder> _traces = new();
    private readonly TimeSpan _traceTimeout = traceTimeout ?? TimeSpan.FromMinutes(5);

    public void AddSpan(SpanModel span)
    {
        TraceBuilder builder = _traces.GetOrAdd(span.TraceId, _ => new(span.TraceId));
        builder.AddSpan(span);

        EvictOldTraces();
    }

    public TraceModel? GetTrace(string traceId) =>
        _traces.TryGetValue(traceId, out TraceBuilder? builder) ? builder.Build() : null;

    public IReadOnlyList<TraceModel> GetRecentTraces(int limit = 100) =>
    [
        .. _traces.Values
            .Select(b => b.Build())
            .OrderByDescending(t => t.StartTime)
            .Take(limit)
    ];

    public IReadOnlyList<TraceModel> Query(TelemetryQuery query)
    {
        IEnumerable<TraceModel> traces = _traces.Values.Select(b => b.Build()).AsEnumerable();

        if (query.ServiceName is not null) traces = traces.Where(t => t.Services.Contains(query.ServiceName));

        if (query.From.HasValue) traces = traces.Where(t => t.StartTime >= query.From.Value);

        if (query.To.HasValue) traces = traces.Where(t => t.EndTime <= query.To.Value);

        return
        [
            .. traces
                .OrderByDescending(t => t.StartTime)
                .Skip(query.Offset)
                .Take(query.Limit)
        ];
    }

    public long TraceCount => _traces.Count;

    private void EvictOldTraces()
    {
        if (_traces.Count <= maxTraces) return;

        DateTimeOffset cutoff = DateTimeOffset.UtcNow - _traceTimeout;
        var toRemove = _traces
            .Where(kv => kv.Value.LastUpdate < cutoff)
            .Select(kv => kv.Key)
            .ToList();

        foreach (string key in toRemove) _traces.TryRemove(key, out _);

        if (_traces.Count > maxTraces)
        {
            var oldest = _traces
                .OrderBy(kv => kv.Value.LastUpdate)
                .Take(_traces.Count - maxTraces)
                .Select(kv => kv.Key)
                .ToList();

            foreach (string key in oldest) _traces.TryRemove(key, out _);
        }
    }

    private sealed class TraceBuilder(string traceId)
    {
        private readonly Lock _lock = new();
        private readonly HashSet<string> _services = [];
        private readonly List<SpanModel> _spans = [];

        public DateTimeOffset LastUpdate { get; private set; } = DateTimeOffset.UtcNow;

        public void AddSpan(SpanModel span)
        {
            lock (_lock)
            {
                _spans.Add(span);
                _services.Add(span.Resource.ServiceName);
                LastUpdate = DateTimeOffset.UtcNow;
            }
        }

        public TraceModel Build()
        {
            lock (_lock)
            {
                if (_spans.Count is 0)
                {
                    return new(
                        traceId,
                        [],
                        DateTimeOffset.MinValue,
                        DateTimeOffset.MinValue,
                        TraceStatus.Ok,
                        new HashSet<string>());
                }

                var sortedSpans = _spans.OrderBy(s => s.StartTime).ToList();
                DateTimeOffset startTime = sortedSpans.Min(s => s.StartTime);
                DateTimeOffset endTime = sortedSpans.Max(s => s.EndTime);
                bool hasError = sortedSpans.Exists(s => s.Status == SpanStatus.Error);

                return new(
                    traceId,
                    sortedSpans,
                    startTime,
                    endTime,
                    hasError ? TraceStatus.Error : TraceStatus.Ok,
                    _services.ToHashSet());
            }
        }
    }
}
