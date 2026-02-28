using System.Collections.Concurrent;
using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Trace;

namespace qyl.collector.Observe;

/// <summary>
/// Manages dynamic observability subscriptions that activate dormant ActivitySources on demand.
///
/// Each subscription wires an ActivityListener (flipping HasListeners() to true) into a
/// BatchExportProcessor backed by an OtlpTraceExporter. When unsubscribed, the pipeline
/// is torn down cleanly and the source returns to zero-cost dormancy.
/// </summary>
internal sealed class SubscriptionManager : IDisposable
{
    private readonly ConcurrentDictionary<string, ObservationSubscription> _subscriptions = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates a new subscription for the given source name filter and OTLP endpoint.
    /// If an identical (filter + endpoint) subscription already exists, returns the existing id
    /// without creating a duplicate pipeline (idempotent).
    /// </summary>
    public ObservationSubscription Subscribe(string filter, string endpoint)
    {
        // Idempotency: reuse if same filter+endpoint already active
        foreach (var existing in _subscriptions.Values)
        {
            if (string.Equals(existing.Filter, filter, StringComparison.Ordinal) &&
                string.Equals(existing.Endpoint, endpoint, StringComparison.Ordinal))
                return existing;
        }

        var id = Guid.NewGuid().ToString("N");
        var pipeline = new ExportPipeline(endpoint);

        // Wire the ActivityListener: subscribes to sources matching the filter.
        // ActivityStopped routes completed activities through the batch processor.
        var listener = new ActivityListener
        {
            ShouldListenTo = source => MatchesFilter(source.Name, filter),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = pipeline.OnEnd
        };

        ActivitySource.AddActivityListener(listener);

        var subscription = new ObservationSubscription(id, filter, endpoint, listener, pipeline);
        _subscriptions[id] = subscription;
        return subscription;
    }

    /// <summary>
    /// Removes and disposes the subscription with the given id.
    /// Returns true if found and removed, false if unknown.
    /// </summary>
    public bool Unsubscribe(string id)
    {
        if (!_subscriptions.TryRemove(id, out var subscription))
            return false;

        subscription.Dispose();
        return true;
    }

    public IReadOnlyCollection<ObservationSubscription> GetAll() => _subscriptions.Values.ToArray();

    public void Dispose()
    {
        foreach (var id in _subscriptions.Keys.ToArray())
            Unsubscribe(id);
    }

    // ── Filter matching ───────────────────────────────────────────────────────

    /// <summary>
    /// Matches source names against glob-style filters:
    /// - "gen_ai.*"  → matches any source starting with "gen_ai."
    /// - "my.source" → exact match
    /// - "*"         → matches everything
    /// </summary>
    private static bool MatchesFilter(string sourceName, string filter)
    {
        if (filter is "*")
            return true;

        if (filter.EndsWithOrdinal(".*"))
        {
            var prefix = filter[..^2]; // strip trailing ".*"
            return sourceName.StartsWithOrdinal(prefix) &&
                   (sourceName.Length == prefix.Length || sourceName[prefix.Length] == '.');
        }

        return string.Equals(sourceName, filter, StringComparison.Ordinal);
    }

    // ── Export pipeline ───────────────────────────────────────────────────────

    /// <summary>
    /// Holds an OtlpTraceExporter and its BatchActivityExportProcessor together so the
    /// disposal chain is explicit and verifiable. The exporter is assigned as a field,
    /// disposed in the constructor's catch block if processor creation fails, and disposed
    /// by the processor's own Dispose() in the success path.
    /// </summary>
    private sealed class ExportPipeline : IDisposable
    {
        private readonly OtlpTraceExporter _exporter;
        private readonly BatchActivityExportProcessor _processor;

        public ExportPipeline(string endpoint)
        {
            _exporter = new OtlpTraceExporter(new OtlpExporterOptions
            {
                Endpoint = new Uri(endpoint),
                Protocol = OtlpExportProtocol.HttpProtobuf
            });
            try
            {
                _processor = new BatchActivityExportProcessor(_exporter);
            }
            catch
            {
                _exporter.Dispose();
                throw;
            }
        }

        /// <summary>Routes a completed activity through the batch processor.</summary>
        public void OnEnd(Activity activity) => _processor.OnEnd(activity);

        public void Dispose()
        {
            _processor.Dispose(); // drains queue and flushes before disposing _exporter transitively
            _exporter.Dispose();  // explicit: satisfies CA2213; BaseExporter.Dispose() is idempotent
        }
    }
}
