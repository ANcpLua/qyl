# qyl.collector - Backend Service

The kernel of qyl. This IS qyl from the user's perspective — the single binary/Docker image that just works.

## Role in Architecture

qyl is an OS in the cloud. The collector is its kernel. Three shells surface the same data:
- **Browser** — dashboard at `:5100` (embedded, no login)
- **Terminal** — `qyl-watch`, Docker logs, CLI
- **IDE** — Copilot + source generator

All three read from the same DuckDB. The collector owns ingest, storage, query, and real-time streaming.

## Upcoming: Error Engine

Priority build to surpass Sentry. Backend engine first, shells light up automatically.

| Feature | Status | Purpose |
|---------|--------|---------|
| Auto crash capture | Planned | AppDomain.UnhandledException + TaskScheduler hooks + ASP.NET middleware |
| Error fingerprinting | Planned | Stack trace normalization + message pattern extraction |
| GenAI-aware grouping | Planned | Group by `gen_ai.operation.name`, finish_reason, token limits — not just stack traces |
| Breadcrumbs | Planned | Passive event trail per scope (ambient HTTP/DB/log activity before crash) |
| Deploy correlation | Planned | Tag spans with release version, auto-resolve on deploy, regression detection |
| AI auto-triage | Planned | MCP tools query grouped errors, correlate with GenAI sessions, suggest root cause |
| SLO burn rate | Planned | Real-time error budget tracking per service/deploy |

## Identity

| Property | Value |
|----------|-------|
| SDK | ANcpLua.NET.Sdk.Web |
| Framework | net10.0 |
| AOT | No (DuckDB native libs) |

## Ports

| Port | Protocol | Purpose |
|------|----------|---------|
| 5100 | HTTP | REST API, SSE, Dashboard |
| 4317 | gRPC | OTLP traces/logs/metrics |

## API Endpoints

**OTLP**: `POST /v1/traces` (HTTP JSON), gRPC `:4317` (TraceService/Export)

**REST**: `/api/v1/traces`, `/api/v1/sessions`, `/api/v1/logs`, `/api/v1/genai/*`

**SSE**: `GET /api/v1/live` (real-time span updates)

**Health**: `/health`, `/health/live`, `/health/ready` (no auth)

**Static**: `/* -> wwwroot/` (embedded dashboard, SPA fallback)

## Storage

DuckDB via `DuckDB.NET.Data.Full`. Tables: `spans`, `logs`, `session_entities`, `errors`.

Write path: channel-buffered single writer, batched multi-row INSERT (100 spans/batch, 200 logs/batch).
Read path: pooled connections with bounded concurrency.

## Structure

```
Auth/           # Token + cookie auth
Grpc/           # gRPC service implementations
Ingestion/      # OTLP parsing, buffering, CORS
Query/          # Query services
Storage/        # DuckDB access (DuckDbStore.cs, DuckDbSchema.g.cs)
Realtime/       # SSE streaming
wwwroot/        # Embedded dashboard
```

## Dependencies

- `qyl.protocol` (ProjectReference)
- `DuckDB.NET.Data.Full`, `Grpc.AspNetCore`, `Google.Protobuf`, `OTelConventions`

## Rules

- DuckDbSchema.g.cs is generated — DO NOT EDIT
- UBIGINT timestamps passed as `decimal` for DuckDB.NET compatibility
- Single writer connection for all writes, pooled read connections
