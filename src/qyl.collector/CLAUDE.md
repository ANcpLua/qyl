# qyl.collector - Backend Service

The kernel of qyl. Single Docker image = entire product. Polyglot OTLP collector (ADR-001).

## Role in Architecture

qyl is an OS in the cloud. The collector is its kernel. Three shells surface the same data:

- **Browser** — dashboard at `:5100` (embedded, no login)
- **Terminal** — `qyl-watch`, Docker logs, CLI
- **IDE** — Copilot + source generator

All three read from the same DuckDB. The collector owns ingest, storage, query, and real-time streaming.

## Upcoming: Error Engine

Priority build to surpass Sentry. Backend engine first, shells light up automatically.

| Feature              | Status  | Purpose                                                                               |
|----------------------|---------|---------------------------------------------------------------------------------------|
| Auto crash capture   | Planned | AppDomain.UnhandledException + TaskScheduler hooks + ASP.NET middleware               |
| Error fingerprinting | Planned | Stack trace normalization + message pattern extraction                                |
| GenAI-aware grouping | Planned | Group by `gen_ai.operation.name`, finish_reason, token limits — not just stack traces |
| Breadcrumbs          | Planned | Passive event trail per scope (ambient HTTP/DB/log activity before crash)             |
| Deploy correlation   | Planned | Tag spans with release version, auto-resolve on deploy, regression detection          |
| AI auto-triage       | Planned | MCP tools query grouped errors, correlate with GenAI sessions, suggest root cause     |
| SLO burn rate        | Planned | Real-time error budget tracking per service/deploy                                    |

## Identity

| Property  | Value                   |
|-----------|-------------------------|
| SDK       | ANcpLua.NET.Sdk.Web     |
| Framework | net10.0                 |
| AOT       | No (DuckDB native libs) |

## Ports

| Port | Protocol | Purpose                  |
|------|----------|--------------------------|
| 5100 | HTTP     | REST API, SSE, Dashboard |
| 4317 | gRPC     | OTLP traces/logs/metrics |

## API Endpoints

**OTLP**: `POST /v1/traces` (HTTP JSON), `POST /v1/logs` (HTTP JSON), gRPC `:4317` (TraceService/Export)

**REST**: `/api/v1/traces`, `/api/v1/sessions`, `/api/v1/logs`, `/api/v1/genai/*`

**Agents Analytics** (ADR implementation — see `PROMPT-AGENTS-DASHBOARD.md`):
- `GET /api/v1/agents/overview/{traffic,duration,issues,llm-calls,tokens,tool-calls}`
- `GET /api/v1/agents/traces` (aggregated trace list)
- `GET /api/v1/agents/models` | `GET /api/v1/agents/tools`
- Common params: `from`, `to`, `project`, `env`, `search`
- Time bucketing: auto (< 24h → 1h, < 7d → 6h, < 30d → 1d, else → 1w)

**Auth** (ADR-002): GitHub OAuth + `QYL_GITHUB_TOKEN` env var. OTLP ingestion (`/v1/traces`, `/v1/logs`) has NO auth gate. CORS defaults to `*`.

**SSE**: `GET /api/v1/live` (real-time span updates)

**Health**: `/health`, `/health/live`, `/health/ready` (no auth)

**Static**: `/* -> wwwroot/` (embedded dashboard, SPA fallback)

## Storage

DuckDB via `DuckDB.NET.Data.Full`. Tables: `spans`, `logs`, `session_entities`, `errors`.

Build failure diagnostics:

- `build_failures` table stores captured `dotnet build/test` failures, binlog paths, and parsed metadata.
- REST endpoints:
    - `POST /api/v1/build-failures`
    - `GET /api/v1/build-failures`
    - `GET /api/v1/build-failures/{id}`
    - `GET /api/v1/build-failures/search?pattern=...`

Structured log source enrichment:

- `logs` now includes `source_file`, `source_line`, `source_column`, `source_method`.
- Enrichment uses normalized `code.*` OTLP attributes first, then stacktrace/PDB best-effort fallback.
- Missing symbols degrade gracefully (null source fields; ingestion still succeeds).

Write path: channel-buffered single writer, batched multi-row INSERT (100 spans/batch, 200 logs/batch).
Read path: pooled connections with bounded concurrency.

## Structure

```
Analytics/      # Agents dashboard query services (ADR implementation)
AgentRuns/      # Agent run extraction + tracking
Auth/           # Token + cookie auth + GitHub OAuth (ADR-002)
Grpc/           # gRPC service implementations
Identity/       # GitHub identity resolution
Ingestion/      # OTLP parsing, buffering, CORS
Query/          # Query services
Storage/        # DuckDB access (DuckDbStore.cs, DuckDbSchema.g.cs)
Realtime/       # SSE streaming
wwwroot/        # Embedded dashboard
```

## Dependencies

- `qyl.protocol` (ProjectReference)
- `DuckDB.NET.Data.Full`, `Grpc.AspNetCore`, `Google.Protobuf`, `OTelConventions`

## DuckDB WriteJob Pattern

- Lambdas that don't capture variables must be `static` (AL0025 analyzer)
- Use `WriteJob<int>` + `_jobs.Writer.WriteAsync` for all writes
- Use `RentReadAsync` for all reads (pooled connections)

## GitHub Identity (ADR-002)

`GitHubService` holds a mutable token behind `Lock`. Three sources (priority order):
1. Runtime token (DuckDB-persisted, set via PAT or Device Flow)
2. Environment variable (`QYL_GITHUB_TOKEN`)
3. None (degraded mode — OTLP still works)

`InitializeAsync()` runs at startup (after DuckDB schema init, before `app.Run()`).

## Rules

- DuckDbSchema.g.cs is generated — DO NOT EDIT
- UBIGINT timestamps passed as `decimal` for DuckDB.NET compatibility
- Single writer connection for all writes, pooled read connections
