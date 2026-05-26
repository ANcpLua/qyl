
using System.Net;
using Microsoft.Extensions.Diagnostics.Enrichment;

namespace Qyl.Collector.Telemetry;

public sealed class QylLogEnricher : ILogEnricher
{
    private readonly string _instanceId = Guid.NewGuid().ToString("N")[..8];

    public void Enrich(IEnrichmentTagCollector collector)
    {
        collector.Add(QylAttr.Auth.InstanceId, _instanceId);

        var activity = Activity.Current;
        if (activity is null) return;

        collector.Add("trace.id", activity.TraceId.ToString());
        collector.Add("span.id", activity.SpanId.ToString());

        foreach (var kv in activity.TagObjects)
        {
            switch (kv.Key)
            {
                case "session.id":
                case "gen_ai.provider.name":
                case "gen_ai.request.model":
                    collector.Add(kv.Key, kv.Value?.ToString() ?? string.Empty);
                    break;
            }
        }
    }
}

public sealed class QylRequestEnricher(IHttpContextAccessor httpContextAccessor) : ILogEnricher
{
    private static readonly IPNetwork[] s_privateRanges =
    [
        IPNetwork.Parse("10.0.0.0/8"),
        IPNetwork.Parse("172.16.0.0/12"),
        IPNetwork.Parse("192.168.0.0/16"),
        IPNetwork.Parse("fc00::/7"),
    ];

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
            collector.Add("client.address", IsLocalAddress(remoteIp) ? remoteIp.ToString() : "[redacted]");
        }

        collector.Add("http.request.method", context.Request.Method);

        if (context.Request.ContentType is not null)
        {
            collector.Add("http.request.content_type", context.Request.ContentType);
        }
    }

    private static bool IsLocalAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address)) return true;
        foreach (var range in s_privateRanges.AsSpan())
        {
            if (range.Contains(address)) return true;
        }
        return false;
    }
}
