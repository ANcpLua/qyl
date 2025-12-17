# ADR-0006: VS-05 Live Streaming

## Metadata

| Field      | Value            |
|------------|------------------|
| Status     | Draft            |
| Date       | 2025-12-16       |
| Slice      | VS-05            |
| Priority   | P2               |
| Depends On | ADR-0002 (VS-01) |
| Supersedes | -                |

## Context

Für Echtzeit-Monitoring benötigt das Dashboard Live-Updates wenn neue Spans ankommen. Polling alle 5s ist nicht
ausreichend für:

- Live Debugging
- Tail-Mode (wie `tail -f`)
- Echtzeit-Dashboards

## Decision

Implementierung von Server-Sent Events (SSE) mit:

- .NET 10 `TypedResults.ServerSentEvents()` API
- Channel<T> für Pub/Sub
- Filter-basiertes Streaming (Service, Session, TraceId)
- Automatische Reconnection im Frontend

## Layers

### 1. TypeSpec (Contract)

```yaml
files:
  - core/specs/api/streaming.tsp      # SSE endpoint definitions
generates:
  - core/generated/openapi/openapi.yaml  # SSE endpoints documented
```

**streaming.tsp Example:**

```typespec
@route("/api/v1/events")
namespace Streaming {
  @get @route("/spans")
  @doc("Stream all new spans via SSE")
  op streamSpans(): EventStream<SpanEvent>;

  @get @route("/spans/filter")
  @doc("Stream filtered spans via SSE")
  op streamFilteredSpans(
    @query serviceName?: string,
    @query sessionId?: string,
    @query traceId?: string,
    @query genAiOnly?: boolean
  ): EventStream<SpanEvent>;
}

model SpanEvent {
  type: "span";
  data: SpanRecord;
  id: string;  // span_id for deduplication
}
```

### 2. Realtime Layer

```yaml
files:
  - src/qyl.collector/Realtime/SseHub.cs              # Channel-based pub/sub
  - src/qyl.collector/Realtime/SseEndpoints.cs        # TypedResults.ServerSentEvents
  - src/qyl.collector/Realtime/TelemetrySseStream.cs  # Stream formatting
  - src/qyl.collector/Contracts/ITelemetrySseBroadcaster.cs  # Abstraction
```

**SseHub Implementation:**

```csharp
public sealed class SseHub : ITelemetrySseBroadcaster, IDisposable
{
    private readonly Channel<SpanRecord> _channel;
    private readonly List<Subscriber> _subscribers = [];
    private readonly Lock _lock = new();

    public SseHub()
    {
        _channel = Channel.CreateBounded<SpanRecord>(new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        _ = ProcessAsync();
    }

    public void PublishSpan(SpanRecord span)
    {
        _channel.Writer.TryWrite(span);
    }

    public IAsyncEnumerable<SpanRecord> SubscribeAsync(
        Func<SpanRecord, bool>? filter,
        CancellationToken ct)
    {
        var subscriber = new Subscriber(filter);

        using (_lock.EnterScope())
        {
            _subscribers.Add(subscriber);
        }

        return subscriber.StreamAsync(ct);
    }

    private async Task ProcessAsync()
    {
        await foreach (var span in _channel.Reader.ReadAllAsync())
        {
            using (_lock.EnterScope())
            {
                foreach (var sub in _subscribers)
                {
                    sub.TryWrite(span);
                }
            }
        }
    }
}
```

### 3. API Layer

```yaml
endpoints:
  - "GET /api/v1/events/spans (SSE)"
  - "GET /api/v1/events/spans/filter?serviceName=&sessionId=&genAiOnly= (SSE)"
files:
  - src/qyl.collector/Realtime/SseEndpoints.cs
```

**Endpoint Implementation:**

```csharp
public static void MapSseEndpoints(this WebApplication app)
{
    app.MapGet("/api/v1/events/spans", async (
        SseHub hub,
        CancellationToken ct) =>
    {
        var stream = hub.SubscribeAsync(filter: null, ct);
        return TypedResults.ServerSentEvents(
            stream.Select(span => new SseItem<SpanRecord>(span, "span")),
            eventType: "span"
        );
    });

    app.MapGet("/api/v1/events/spans/filter", async (
        SseHub hub,
        string? serviceName,
        string? sessionId,
        string? traceId,
        bool? genAiOnly,
        CancellationToken ct) =>
    {
        Func<SpanRecord, bool> filter = span =>
            (serviceName is null || span.ServiceName == serviceName) &&
            (sessionId is null || span.SessionId == sessionId) &&
            (traceId is null || span.TraceId == traceId) &&
            (!genAiOnly.GetValueOrDefault() || span.GenAiOperationName is not null);

        var stream = hub.SubscribeAsync(filter, ct);
        return TypedResults.ServerSentEvents(
            stream.Select(span => new SseItem<SpanRecord>(span, "span")),
            eventType: "span"
        );
    });
}
```

### 4. MCP Layer

```yaml
note: "VS-05 hat keine MCP Tools (SSE ist für Dashboard, nicht CLI)"
```

### 5. Dashboard Layer

```yaml
files:
  - src/qyl.dashboard/src/hooks/useSse.ts            # useSpanStream() hook
  - src/qyl.dashboard/src/hooks/useQueryInvalidation.ts  # SSE-triggered refresh
  - src/qyl.dashboard/src/components/live/LiveIndicator.tsx
  - src/qyl.dashboard/src/components/live/LiveSpanFeed.tsx
patterns:
  - "EventSource API mit Reconnection"
  - "SSE triggers queryClient.invalidateQueries()"
  - "Optimistic Updates für bessere UX"
```

**useSse.ts Implementation:**

```typescript
export function useSpanStream(options?: {
  serviceName?: string;
  sessionId?: string;
  genAiOnly?: boolean;
  onSpan?: (span: SpanRecord) => void;
}) {
  const queryClient = useQueryClient();
  const [isConnected, setIsConnected] = useState(false);

  useEffect(() => {
    const params = new URLSearchParams();
    if (options?.serviceName) params.set('serviceName', options.serviceName);
    if (options?.sessionId) params.set('sessionId', options.sessionId);
    if (options?.genAiOnly) params.set('genAiOnly', 'true');

    const url = params.toString()
      ? `/api/v1/events/spans/filter?${params}`
      : '/api/v1/events/spans';

    const eventSource = new EventSource(url);

    eventSource.onopen = () => setIsConnected(true);
    eventSource.onerror = () => setIsConnected(false);

    eventSource.addEventListener('span', (event) => {
      const span = JSON.parse(event.data) as SpanRecord;
      options?.onSpan?.(span);

      // Invalidate relevant queries
      queryClient.invalidateQueries({ queryKey: ['sessions'] });
      queryClient.invalidateQueries({ queryKey: ['traces', span.traceId] });
    });

    return () => eventSource.close();
  }, [options?.serviceName, options?.sessionId, options?.genAiOnly]);

  return { isConnected };
}
```

## Acceptance Criteria

- [ ] `GET /api/v1/events/spans` öffnet SSE Connection
- [ ] Neue Spans werden sofort an verbundene Clients gesendet
- [ ] Filter (serviceName, sessionId, genAiOnly) funktionieren
- [ ] Dashboard zeigt Live-Indicator (grün wenn verbunden)
- [ ] Dashboard aktualisiert Listen automatisch bei neuen Spans
- [ ] Reconnection bei Connection-Loss (max 5 Retries)
- [ ] Memory-Leak-frei (Subscribers werden aufgeräumt)
- [ ] Backpressure: DropOldest bei Channel-Overflow

## Test Files

```yaml
unit_tests:
  - tests/qyl.collector.tests/Realtime/SseHubTests.cs
integration_tests:
  - tests/qyl.collector.tests/Realtime/SseEndpointsTests.cs
```

## Consequences

### Positive

- **Echtzeit-Updates**: Keine Verzögerung durch Polling
- **Reduzierte Last**: Nur Changes werden gesendet (nicht alle Daten)
- **Bessere UX**: Sofortiges Feedback beim Debugging

### Negative

- **Connection-Management**: Viele offene Connections bei vielen Clients
- **Browser-Limits**: Max 6 SSE Connections per Domain
- **Memory**: Channel hält Spans bis alle Subscriber verarbeitet haben

### Risks

| Risk                 | Impact | Likelihood | Mitigation                             |
|----------------------|--------|------------|----------------------------------------|
| Memory Leak          | High   | Medium     | Bounded Channel + Subscriber Cleanup   |
| Too Many Connections | Medium | Low        | Connection Pooling / WebSocket Upgrade |
| Event Loss           | Low    | Medium     | Event ID für Client-Side Deduplication |

## References

- [ADR-0002](0002-vs01-span-ingestion.md) - VS-01 Span Ingestion (Dependency)
- [TypedResults.ServerSentEvents](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses) -
  .NET 10 API
- [MDN EventSource](https://developer.mozilla.org/en-US/docs/Web/API/EventSource)
- [qyl-architecture.yaml](../../qyl-architecture.yaml) Section: patterns.web_api
