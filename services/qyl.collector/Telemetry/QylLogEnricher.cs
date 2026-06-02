
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

        if (activity.GetTagItem("session.id") is { } sessionId)
            collector.Add("session.id", sessionId.ToString() ?? string.Empty);
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
