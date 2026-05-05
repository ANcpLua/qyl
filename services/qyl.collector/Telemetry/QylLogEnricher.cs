
using Microsoft.Extensions.Diagnostics.Enrichment;

namespace Qyl.Collector.Telemetry;

public sealed class QylLogEnricher : ILogEnricher
{
    private readonly string _instanceId = Guid.NewGuid().ToString("N")[..8];

    public void Enrich(IEnrichmentTagCollector collector)
    {
        collector.Add(QylAttr.Auth.InstanceId, _instanceId);

        var activity = Activity.Current;
        if (activity is not null)
        {
            collector.Add("trace.id", activity.TraceId.ToString());
            collector.Add("span.id", activity.SpanId.ToString());

            if (activity.GetTagItem("session.id") is { } sessionId)
            {
                collector.Add("session.id", sessionId.ToString() ?? string.Empty);
            }

            if (activity.GetTagItem("gen_ai.provider.name") is { } provider)
            {
                collector.Add("gen_ai.provider.name", provider.ToString() ?? string.Empty);
            }

            if (activity.GetTagItem("gen_ai.request.model") is { } model)
            {
                collector.Add("gen_ai.request.model", model.ToString() ?? string.Empty);
            }
        }
    }
}

public sealed class QylRequestEnricher(IHttpContextAccessor httpContextAccessor) : ILogEnricher
{
    public void Enrich(IEnrichmentTagCollector collector)
    {
        if (httpContextAccessor.HttpContext is not { } context) return;

        collector.Add("http.request.id", context.TraceIdentifier);

        var endpoint = context.GetEndpoint();
        if (endpoint is not null)
        {
            collector.Add("http.route", endpoint.DisplayName ?? "unknown");
        }

        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp is not null)
        {
            var ipString = remoteIp.ToString();
            if (ipString.StartsWithOrdinal("127.") ||
                ipString.StartsWithOrdinal("::1") ||
                ipString.StartsWithOrdinal("10.") ||
                ipString.StartsWithOrdinal("192.168."))
            {
                collector.Add("client.address", ipString);
            }
            else
            {
                collector.Add("client.address", "[redacted]");
            }
        }

        collector.Add("http.request.method", context.Request.Method);

        if (context.Request.ContentType is not null)
        {
            collector.Add("http.request.content_type", context.Request.ContentType);
        }
    }
}
