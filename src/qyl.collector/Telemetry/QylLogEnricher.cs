// =============================================================================
// qyl Log Enricher - .NET 10 ILogEnricher Implementation
// Automatically adds context to all log entries
// =============================================================================

using Microsoft.Extensions.Diagnostics.Enrichment;

namespace qyl.collector.Telemetry;

public sealed class QylLogEnricher : ILogEnricher
{
    private readonly string _instanceId = Guid.NewGuid().ToString("N")[..8];

    public void Enrich(IEnrichmentTagCollector collector)
    {
        // Instance identification
        collector.Add("qyl.instance_id", _instanceId);

        // Add current activity context if available
        var activity = Activity.Current;
        if (activity is not null)
        {
            collector.Add("trace.id", activity.TraceId.ToString());
            collector.Add("span.id", activity.SpanId.ToString());

            // Extract session.id if present
            if (activity.GetTagItem("session.id") is { } sessionId)
            {
                collector.Add("session.id", sessionId.ToString() ?? string.Empty);
            }

            // Extract GenAI context if present
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

/// <summary>
///     Enriches log entries with request context from HttpContext.
/// </summary>
public sealed class QylRequestEnricher : ILogEnricher
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public QylRequestEnricher(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    public void Enrich(IEnrichmentTagCollector collector)
    {
        if (_httpContextAccessor.HttpContext is not { } context) return;

        // Request identification
        collector.Add("http.request.id", context.TraceIdentifier);

        // Route information
        var endpoint = context.GetEndpoint();
        if (endpoint is not null)
        {
            collector.Add("http.route", endpoint.DisplayName ?? "unknown");
        }

        // Client information (if not PII-sensitive)
        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp is not null)
        {
            // Only log local/internal IPs, redact external
            if (remoteIp.ToString().StartsWith("127.") ||
                remoteIp.ToString().StartsWith("::1") ||
                remoteIp.ToString().StartsWith("10.") ||
                remoteIp.ToString().StartsWith("192.168."))
            {
                collector.Add("client.address", remoteIp.ToString());
            }
            else
            {
                collector.Add("client.address", "[redacted]");
            }
        }

        // Request method and path
        collector.Add("http.request.method", context.Request.Method);

        // Content type for POST requests
        if (context.Request.ContentType is not null)
        {
            collector.Add("http.request.content_type", context.Request.ContentType);
        }
    }
}
