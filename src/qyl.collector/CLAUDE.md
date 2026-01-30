# qyl.collector - Backend Service

Primary runtime service. This IS qyl from the user's perspective.

## Identity

| Property | Value |
|----------|-------|
| SDK | ANcpLua.NET.Sdk.Web |
| Framework | net10.0 |
| Role | primary-runtime |
| AOT | **No** (DuckDB native libs) |

## Ports

| Port | Protocol | Purpose |
|------|----------|---------|
| 5100 | HTTP | REST API, SSE streaming, Dashboard |
| 4317 | gRPC | OTLP traces/logs/metrics |

## API Endpoints

### OTLP Ingestion

```
POST /v1/traces           # OTLP HTTP JSON
gRPC :4317                # TraceService/Export
```

### REST API

```
GET  /api/v1/traces                    # List traces
GET  /api/v1/traces/{traceId}          # Get trace
GET  /api/v1/traces/{traceId}/spans    # Get trace spans
POST /api/v1/traces/search             # Search traces

GET  /api/v1/sessions                  # List sessions
GET  /api/v1/sessions/{id}             # Get session
GET  /api/v1/sessions/{id}/traces      # Get session traces

GET  /api/v1/logs                      # Query logs
POST /api/v1/logs/search               # Search logs
GET  /api/v1/logs/stats                # Log statistics

GET  /api/v1/errors                    # List errors
GET  /api/v1/errors/{id}               # Get error
GET  /api/v1/errors/stats              # Error statistics

GET  /health                           # Health check
GET  /health/live                      # Liveness probe
GET  /health/ready                     # Readiness probe
```

### SSE Streaming

```
GET /api/v1/live                       # Real-time span updates
```

### Static Files

```
/* -> wwwroot/                         # Embedded dashboard (SPA fallback)
```

## Storage

| Property | Value |
|----------|-------|
| Engine | DuckDB |
| Package | DuckDB.NET.Data.Full |
| Location | `$QYL_DATA_PATH` or `./qyl.duckdb` |

### Tables

| Table | Purpose |
|-------|---------|
| `spans` | Primary telemetry data |
| `logs` | Log records |
| `session_entities` | Aggregated session stats |
| `errors` | Error entities |

## Key Patterns

### Ingestion Pipeline

```csharp
// Channel-based buffered ingestion
Channel<SpanRecord> _channel = Channel.CreateBounded<SpanRecord>(
    new BoundedChannelOptions(10000) { FullMode = BoundedChannelFullMode.DropOldest });

// Background writer
await foreach (var batch in _channel.Reader.ReadAllAsync(ct).Chunk(100))
{
    await _duckDb.InsertBatchAsync(batch, ct);
}
```

### Time Handling

```csharp
// Use TimeProvider for testability
private readonly TimeProvider _timeProvider = TimeProvider.System;
var now = _timeProvider.GetUtcNow();

// DuckDB uses ulong for timestamps
ulong timestampNanos = (ulong)span.StartTimeUnixNano;
```

### JSON Serialization (CA1869)

```csharp
private static readonly JsonSerializerOptions s_otlpOptions = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};
```

### Locking

```csharp
// Sync context
private readonly Lock _lock = new();
using (_lock.EnterScope()) { /* critical section */ }

// Async context
private readonly SemaphoreSlim _asyncLock = new(1, 1);
await _asyncLock.WaitAsync(ct);
try { /* async critical section */ }
finally { _asyncLock.Release(); }
```

## Dependencies

### Project References

- `qyl.protocol` - Shared types

### Packages

- `DuckDB.NET.Data.Full` - Database
- `Grpc.AspNetCore` - gRPC server
- `Google.Protobuf` - Protobuf serialization
- `OTelConventions` - OTel attribute constants

## Environment Variables

| Variable | Default | Purpose |
|----------|---------|---------|
| `QYL_PORT` | 5100 | HTTP port |
| `QYL_GRPC_PORT` | 4317 | gRPC port (0 to disable) |
| `QYL_DATA_PATH` | ./qyl.duckdb | Database file path |

## Directory Structure

```
Api/                    # REST API endpoints
Grpc/                   # gRPC service implementations
Ingestion/              # OTLP parsing and buffering
Query/                  # Query services
Storage/                # DuckDB access layer
  DuckDbSchema.g.cs     # Generated schema - DO NOT EDIT
wwwroot/                # Embedded dashboard (copied at build)
Program.cs              # Application entry point
```
