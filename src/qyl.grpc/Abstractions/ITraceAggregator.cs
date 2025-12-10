using qyl.grpc.Models;

namespace qyl.grpc.Abstractions;

public interface ITraceAggregator
{
    void AddSpan(SpanModel span);
    TraceModel? GetTrace(string traceId);
    IReadOnlyList<TraceModel> GetRecentTraces(int limit = 100);
    IReadOnlyList<TraceModel> Query(TelemetryQuery query);
    long TraceCount { get; }
}
