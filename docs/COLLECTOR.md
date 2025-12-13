# Collector

> Backend: OTLP ingestion → DuckDB → REST/SSE

## Structure

```
src/qyl.collector/
├── Program.cs                 # Entry, DI, endpoints
├── Auth/
│   ├── TokenAuth.cs           # Optional auth middleware
│   └── TokenGenerator.cs
├── Ingestion/
│   ├── OtlpJsonSpanParser.cs  # Parse OTLP JSON
│   ├── OtlpTypes.cs           # Protobuf types
│   └── SchemaNormalizer.cs    # Migrate deprecated attrs
├── Storage/
│   ├── DuckDbStore.cs         # ISpanStore implementation
│   └── DuckDbSchema.cs        # DDL (SINGLE SOURCE)
├── Query/
│   └── SessionQueryService.cs # Session aggregation
├── Realtime/
│   ├── SseEndpoints.cs        # GET /api/v1/events/*
│   ├── SseHub.cs              # Channel-based pub/sub
│   └── TelemetrySseStream.cs  # IAsyncEnumerable writer
├── GenAiAttributes.cs         # OTel constants
├── GenAiExtractor.cs          # Extract gen_ai.* from spans
└── QylSerializerContext.cs    # AOT JSON context
```

## Ports

| Port | Protocol | Purpose |
|------|----------|---------|
| 5100 | HTTP | REST API |
| 4318 | HTTP | OTLP/HTTP (protobuf) |
| 4317 | gRPC | OTLP/gRPC |

## API Endpoints

### OTLP Ingestion
```
POST /v1/traces          # OTLP HTTP (application/x-protobuf)
gRPC TraceService/Export # OTLP gRPC
```

### REST API
```
GET /api/v1/sessions              # List sessions
GET /api/v1/sessions/{sessionId}  # Get session details
GET /api/v1/traces/{traceId}      # Get trace tree
GET /api/v1/spans                 # Query spans
    ?serviceName=myapp
    &from=2024-01-01T00:00:00Z
    &to=2024-01-02T00:00:00Z
    &genAiOnly=true
    &limit=100
```

### SSE Streaming
```
GET /api/v1/events/spans          # Real-time span stream
    event: span
    data: {"traceId":"...","spanId":"..."}
```

### Health
```
GET /health                       # {"status":"healthy"}
```

## DuckDB Schema

```sql
CREATE TABLE IF NOT EXISTS spans (
    trace_id              VARCHAR(32) NOT NULL,
    span_id               VARCHAR(16) NOT NULL,
    parent_span_id        VARCHAR(16),
    session_id            VARCHAR(32),
    name                  VARCHAR NOT NULL,
    kind                  UTINYINT,
    start_time_unix_nano  BIGINT NOT NULL,
    end_time_unix_nano    BIGINT NOT NULL,
    duration_ns           BIGINT GENERATED ALWAYS AS
                          (end_time_unix_nano - start_time_unix_nano),
    status_code           UTINYINT,
    status_message        VARCHAR,

    -- Promoted gen_ai.* columns
    "gen_ai.provider.name"        VARCHAR,
    "gen_ai.request.model"        VARCHAR,
    "gen_ai.response.model"       VARCHAR,
    "gen_ai.operation.name"       VARCHAR,
    "gen_ai.usage.input_tokens"   BIGINT,
    "gen_ai.usage.output_tokens"  BIGINT,

    -- Overflow
    attributes            MAP(VARCHAR, VARCHAR),
    events                JSON,
    links                 JSON,

    PRIMARY KEY (trace_id, span_id)
);
```

## Configuration

Environment variables:
```bash
DuckDb__Path=/data/qyl.duckdb    # Database file path
QYL_TOKEN=secret                  # Optional auth token
ASPNETCORE_URLS=http://+:5100    # Listen address
```

## Run

```bash
# Development
dotnet run --project src/qyl.collector

# Docker
docker run -p 5100:5100 -p 4318:4318 ghcr.io/qyl/collector
```
