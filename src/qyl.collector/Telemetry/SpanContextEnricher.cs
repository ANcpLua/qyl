// =============================================================================
// qyl Span Context Enricher - Adds ingestion context to all logs
// Uses IAsyncContext<IngestionContext> for request-scoped state
// =============================================================================

using Microsoft.Extensions.AsyncState;
using Microsoft.Extensions.Diagnostics.Enrichment;

namespace qyl.collector.Telemetry;

/// <summary>
///     Log enricher that adds ingestion context tags to all logs.
///     Uses AsyncState to access request-scoped context across async boundaries.
/// </summary>
public sealed class SpanContextEnricher(IAsyncContext<IngestionContext> context) : ILogEnricher
{
    /// <inheritdoc />
    public void Enrich(IEnrichmentTagCollector collector)
    {
        if (context.Get() is not { } ctx)
            return;

        if (ctx.SessionId is not null)
            collector.Add("qyl.session.id", ctx.SessionId);

        if (ctx.ServiceName is not null)
            collector.Add("qyl.service.name", ctx.ServiceName);

        if (ctx.Provider is not null)
            collector.Add("gen_ai.provider.name", ctx.Provider);

        if (ctx.Model is not null)
            collector.Add("gen_ai.request.model", ctx.Model);

        if (ctx.TraceId is not null)
            collector.Add("qyl.trace.id", ctx.TraceId);

        if (ctx.SpanCount > 0)
            collector.Add("qyl.span.count", ctx.SpanCount);
    }
}
