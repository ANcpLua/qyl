
using Microsoft.Extensions.Diagnostics.Enrichment;
using Microsoft.AspNetCore.Routing;
using Qyl.Collector.Ingestion;

namespace Qyl.Collector.Telemetry;

internal sealed class QylLogEnricher : ILogEnricher
{
    private readonly string _instanceId = Guid.NewGuid().ToString("N")[..8];

    public void Enrich(IEnrichmentTagCollector collector)
    {
        collector.Add(QylAttr.Auth.InstanceId, _instanceId);
    }
}

internal sealed class QylRequestEnricher(IHttpContextAccessor httpContextAccessor) : ILogEnricher
{
    public void Enrich(IEnrichmentTagCollector collector)
    {
        if (httpContextAccessor.HttpContext is not { } context) return;

        if (context.GetEndpoint() is RouteEndpoint { RoutePattern.RawText.Length: > 0 } routeEndpoint)
        {
            collector.Add(CollectorSemanticAttributeCatalog.HttpRoute, routeEndpoint.RoutePattern.RawText);
        }

        collector.Add(CollectorSemanticAttributeCatalog.HttpRequestMethod, context.Request.Method);
    }
}
