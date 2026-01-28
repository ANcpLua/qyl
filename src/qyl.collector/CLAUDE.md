# qyl.collector

Backend service. This IS qyl from user perspective.

## identity

```yaml
sdk: ANcpLua.NET.Sdk.Web
role: primary-runtime
ports: [5100 (http), 4317 (grpc)]
```

## endpoints

```yaml
otlp:
  - POST /v1/traces (OTLP HTTP JSON)
  - gRPC :4317 TraceService/Export

rest:
  - GET /api/v1/sessions
  - GET /api/v1/sessions/{id}
  - GET /api/v1/sessions/{id}/spans
  - GET /api/v1/traces/{traceId}
  - GET /api/v1/stats/tokens
  - GET /api/v1/stats/latency
  - GET /health, /alive (Aspire standard)

streaming:
  - GET /api/v1/live (SSE)

static:
  - /* -> wwwroot/ (embedded dashboard)
```

## storage

```yaml
engine: DuckDB
package: DuckDB.NET.Data.Full
location: $QYL_DATA_PATH or ./qyl.duckdb

tables:
  spans: primary telemetry data
  sessions: aggregated session stats
  logs: log records
```

## patterns

```yaml
channel: Channel<SpanRecord> for ingestion buffer
json: static readonly JsonSerializerOptions (CA1869)
time: TimeProvider.System, unix nanoseconds
locking:
  sync: Lock + EnterScope()
  async: SemaphoreSlim
```

## dependencies

```yaml
project: qyl.protocol
packages:
  - DuckDB.NET.Data.Full
  - Grpc.AspNetCore
  - Google.Protobuf
  - OTelConventions
```
