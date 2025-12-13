# qyl.collector — Backend

@../../CLAUDE.md

## Purpose

The qyl backend that:
- Receives OTLP telemetry (gRPC :4317, HTTP :4318)
- Extracts `gen_ai.*` attributes from spans
- Stores data in embedded DuckDB
- Exposes REST API for queries
- Streams updates via SSE

**This is NOT an OpenTelemetry Collector** — it's a destination/backend, not a pipeline processor.

## Hard Rules

| ✅ MAY Reference | ❌ MUST NOT Reference |
|-----------------|----------------------|
| `qyl.protocol` | `qyl.mcp` |
| `DuckDB.NET.Data` | `qyl.dashboard` |
| `Google.Protobuf` | `OpenTelemetry.Exporter.*` |
| `Grpc.AspNetCore` | Any "SDK" packages |

**Critical**:
- Collector RECEIVES telemetry, it doesn't EXPORT
- No MCP code in collector — that's in `qyl.mcp`
- No `wwwroot/` — dashboard is separate

## Structure

```
qyl.collector/
├── qyl.collector.csproj
├── Program.cs                    # Entry point, DI, endpoints
├── Ingestion/
│   ├── OtlpHttpReceiver.cs       # POST /v1/traces, /v1/metrics, /v1/logs
│   ├── OtlpGrpcService.cs        # gRPC TraceService
│   ├── GenAiExtractor.cs         # Extract gen_ai.* → GenAiSpanData
│   └── SpanConverter.cs          # OTLP Span → SpanRecord
├── Storage/
│   ├── DuckDbStore.cs            # ISpanStore implementation
│   └── DuckDbSchema.cs           # DDL, migrations
├── Query/
│   ├── SessionAggregator.cs      # ISessionAggregator implementation
│   └── TraceBuilder.cs           # Build hierarchical traces
├── Api/
│   ├── SessionsEndpoint.cs       # GET /api/v1/sessions
│   ├── TracesEndpoint.cs         # GET /api/v1/traces/{traceId}
│   ├── SpansEndpoint.cs          # GET /api/v1/spans
│   └── SseEndpoint.cs            # GET /api/v1/events/spans
├── Realtime/
│   ├── SpanBroadcaster.cs        # Channel-based pub/sub
│   └── SseWriter.cs              # SSE formatting
├── appsettings.json
└── Dockerfile
```

## Ports

| Port | Protocol | Purpose |
|------|----------|---------|
| 8080 | HTTP | REST API + SSE |
| 4317 | gRPC | OTLP traces/metrics/logs |
| 4318 | HTTP | OTLP traces/metrics/logs |

## Endpoints

### OTLP Ingestion
```
POST /v1/traces          # OTLP HTTP (protobuf)
POST /v1/metrics         # OTLP HTTP (protobuf)
POST /v1/logs            # OTLP HTTP (protobuf)
gRPC TraceService.Export # OTLP gRPC
```

### REST API
```
GET /api/v1/sessions                    # List sessions
GET /api/v1/sessions/{sessionId}        # Get session
GET /api/v1/traces/{traceId}            # Get trace tree
GET /api/v1/spans?serviceName=&from=&to=&genAiOnly=&limit=
```

### Streaming
```
GET /api/v1/events/spans                # SSE stream of new spans
```

### Health
```
GET /health                             # { status: "healthy" }
```

## Key Components

### GenAiExtractor
Extracts `gen_ai.*` attributes from OTLP spans:

```csharp
public GenAiSpanData? Extract(Span span)
{
    var attrs = span.Attributes.ToDictionary(a => a.Key, a => a.Value);
    
    if (!attrs.TryGetValue(GenAiAttributes.OperationName, out var opName))
        return null;  // Not a gen_ai span
    
    return new GenAiSpanData
    {
        OperationName = opName.StringValue,
        Provider = attrs.GetValueOrDefault(GenAiAttributes.ProviderName)?.StringValue,
        InputTokens = (int?)attrs.GetValueOrDefault(GenAiAttributes.UsageInputTokens)?.IntValue,
        // ...
    };
}
```

### DuckDbStore
Thread-safe storage using `System.Threading.Lock`:

```csharp
public sealed class DuckDbStore : ISpanStore
{
    private readonly Lock _lock = new();  // .NET 9+ Lock class
    
    public async Task InsertSpansAsync(IReadOnlyList<SpanRecord> spans, CancellationToken ct)
    {
        using (_lock.EnterScope())
        {
            using var appender = _connection.CreateAppender("spans");
            foreach (var span in spans)
            {
                appender.CreateRow()
                    .AppendValue(span.TraceId)
                    // ...
                    .EndRow();
            }
        }
    }
}
```

### SpanBroadcaster
Channel-based pub/sub for SSE:

```csharp
public sealed class SpanBroadcaster
{
    private readonly Channel<SpanRecord> _channel = Channel.CreateBounded<SpanRecord>(10_000);
    
    public async ValueTask PublishAsync(SpanRecord span, CancellationToken ct)
        => await _channel.Writer.WriteAsync(span, ct);
    
    public IAsyncEnumerable<SpanRecord> SubscribeAsync(CancellationToken ct)
        => _channel.Reader.ReadAllAsync(ct);
}
```

### SseEndpoint (.NET 10)
```csharp
app.MapGet("/api/v1/events/spans", (SpanBroadcaster broadcaster, CancellationToken ct) =>
    TypedResults.ServerSentEvents(
        broadcaster.SubscribeAsync(ct),
        eventType: "span"));
```

## Configuration

```json
{
  "DuckDb": {
    "Path": "qyl.db"
  },
  "Kestrel": {
    "Endpoints": {
      "Http": { "Url": "http://*:8080" },
      "Grpc": { "Url": "http://*:4317", "Protocols": "Http2" },
      "OtlpHttp": { "Url": "http://*:4318" }
    }
  }
}
```

## Docker

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080 4317 4318
ENTRYPOINT ["dotnet", "qyl.collector.dll"]
```

## Required Patterns

- Use `Lock` class for thread synchronization (not `object` + `lock`)
- Use `TimeProvider.System` for timestamps (not `DateTime.Now`)
- Use `FrozenDictionary` for attribute lookups
- Use `TypedResults.ServerSentEvents()` for SSE
- Use Appender API for DuckDB bulk inserts
