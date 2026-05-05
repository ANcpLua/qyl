using OpenTelemetry;
using OpenTelemetry.Exporter;
using Qyl.Contracts.Generated;

namespace Qyl.Collector.Observe;

[QylService(QylLifetime.Singleton)]
internal sealed class SubscriptionManager : IDisposable
{
    private readonly ConcurrentDictionary<string, ObservationSubscription> _subscriptions = new(StringComparer.Ordinal);

    public void Dispose()
    {
        foreach (var id in _subscriptions.Keys.ToArray())
            Unsubscribe(id);
    }

    public ObservationSubscription Subscribe(string filter, string endpoint, string? schemaVersion)
    {
        foreach (var existing in _subscriptions.Values)
        {
            if (string.Equals(existing.Filter, filter, StringComparison.Ordinal) &&
                string.Equals(existing.Endpoint, endpoint, StringComparison.Ordinal))
                return existing;
        }

        var id = Guid.NewGuid().ToString("N");
        var contractHash = ResolveContractHash(filter);
        var pipeline = new ExportPipeline(endpoint);

        var listener = new ActivityListener
        {
            ShouldListenTo = source => MatchesFilter(source.Name, filter),
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = pipeline.OnEnd
        };

        ActivitySource.AddActivityListener(listener);

        var subscription = new ObservationSubscription(
            id, filter, endpoint, listener, pipeline, contractHash, schemaVersion);
        _subscriptions[id] = subscription;
        return subscription;
    }

    public ObservationSubscription Subscribe(string filter, string endpoint)
        => Subscribe(filter, endpoint, null);

    public bool Unsubscribe(string id)
    {
        if (!_subscriptions.TryRemove(id, out var subscription))
            return false;

        subscription.Dispose();
        return true;
    }

    public IReadOnlyCollection<ObservationSubscription> GetAll() => _subscriptions.Values.ToArray();


    private static bool MatchesFilter(string sourceName, string filter)
    {
        if (filter is "*")
            return true;

        if (filter.EndsWithOrdinal(".*"))
        {
            var prefix = filter[..^2];
            return sourceName.StartsWithOrdinal(prefix) &&
                   (sourceName.Length == prefix.Length || sourceName[prefix.Length] == '.');
        }

        return string.Equals(sourceName, filter, StringComparison.Ordinal);
    }

    private static string? ResolveContractHash(string filter)
    {
        string? matchedSource = null;
        foreach (var domain in DomainContracts.All)
        {
            if (!MatchesFilter(domain.Source, filter))
                continue;

            if (matchedSource is not null)
                return null;

            matchedSource = domain.Source;
        }

        return matchedSource is not null
            ? ObserveCatalog.GetDomainHash(matchedSource)
            : null;
    }


    private sealed class ExportPipeline : IDisposable
    {
        private readonly OtlpTraceExporter _exporter;
        private readonly BatchActivityExportProcessor _processor;

        public ExportPipeline(string endpoint)
        {
            _exporter = new OtlpTraceExporter(new OtlpExporterOptions
            {
                Endpoint = new Uri(endpoint), Protocol = OtlpExportProtocol.HttpProtobuf
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

        public void Dispose()
        {
            _processor.Dispose();
            _exporter.Dispose();
        }

        public void OnEnd(Activity activity) => _processor.OnEnd(activity);
    }
}
