
using Microsoft.Extensions.Diagnostics.Enrichment;
using HttpAttributes = Qyl.OpenTelemetry.SemanticConventions.Attributes.Http.HttpAttributes;

namespace Qyl.Collector.Telemetry;

public sealed class QylLogEnricher : ILogEnricher
{
    private readonly string _instanceId = Guid.NewGuid().ToString("N")[..8];

    public void Enrich(IEnrichmentTagCollector collector)
    {
        collector.Add(QylAttr.Auth.InstanceId, _instanceId);
    }
}

public sealed class QylRequestEnricher(IHttpContextAccessor httpContextAccessor) : ILogEnricher
{
    public void Enrich(IEnrichmentTagCollector collector)
    {
        if (httpContextAccessor.HttpContext is not { } context) return;

        var endpoint = context.GetEndpoint();
        if (endpoint is not null)
        {
            collector.Add(HttpAttributes.Route, endpoint.DisplayName ?? "unknown");
        }

        collector.Add(HttpAttributes.RequestMethod, context.Request.Method);
    }
}
