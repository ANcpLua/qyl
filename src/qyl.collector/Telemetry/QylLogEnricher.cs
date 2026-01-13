// =============================================================================
// qyl Log Enricher - .NET 10 ILogEnricher Implementation
// Automatically adds context to all log entries
// =============================================================================

using Microsoft.Extensions.Diagnostics.Enrichment;

namespace qyl.collector.Telemetry;

/// <summary>
///     Enriches all log entries with qyl-specific context.
///     .NET 8+ feature: ILogEnricher for automatic log enrichment.
/// </summary>
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
            var sessionId = activity.GetTagItem("session.id");
            if (sessionId is not null)
            {
                collector.Add("session.id", sessionId.ToString()!);
            }

            // Extract GenAI context if present
            var provider = activity.GetTagItem("gen_ai.provider.name");
            if (provider is not null)
            {
                collector.Add("gen_ai.provider.name", provider.ToString()!);
            }

            var model = activity.GetTagItem("gen_ai.request.model");
            if (model is not null)
            {
                collector.Add("gen_ai.request.model", model.ToString()!);
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
        var context = _httpContextAccessor.HttpContext;
        if (context is null) return;

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

/// <summary>
///     Enriches log entries with storage context.
/// </summary>
public sealed class QylStorageEnricher : ILogEnricher
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);
    private readonly Func<long> _getSessionCount;
    private readonly Func<long> _getSpanCount;
    private readonly TimeProvider _timeProvider;
    private long _lastSessionCount;
    private long _lastSpanCount;
    private DateTimeOffset _lastUpdate = DateTimeOffset.MinValue;

    public QylStorageEnricher(Func<long> getSpanCount, Func<long> getSessionCount, TimeProvider? timeProvider = null)
    {
        _getSpanCount = getSpanCount;
        _getSessionCount = getSessionCount;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public void Enrich(IEnrichmentTagCollector collector)
    {
        // Cache expensive storage queries
        var now = _timeProvider.GetUtcNow();
        if (now - _lastUpdate > CacheDuration)
        {
            _lastSpanCount = _getSpanCount();
            _lastSessionCount = _getSessionCount();
            _lastUpdate = now;
        }

        collector.Add("storage.span_count", _lastSpanCount);
        collector.Add("storage.session_count", _lastSessionCount);
    }
}
