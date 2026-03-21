# qyl

**Question Your Logs** — AI Observability Platform

Landing page: <https://ancplua.github.io/qyl/>

## What qyl Does

|                 |                                                                     |
|-----------------|---------------------------------------------------------------------|
| **Collects**    | OTLP receiver with idempotent ingestion (retry-safe)                |
| **Instruments** | Roslyn source generators for zero-config GenAI telemetry            |
| **Visualizes**  | Real-time dashboard with SSE streaming                              |
| **Integrates**  | MCP surface, Loom agents, and GitOps over a shared application host |

## Tech Stack

| Layer    | Technology                                    |
|----------|-----------------------------------------------|
| Runtime  | .NET 10.0 LTS, C# 14                          |
| Frontend | React 19, Vite 7, Tailwind CSS 4              |
| Storage  | DuckDB (columnar, upsert-based)               |
| Protocol | OpenTelemetry 1.40 GenAI Semantic Conventions |
| Schema   | TypeSpec → OpenAPI → C#/DuckDB/TypeScript     |

## Components

| Package                          | Purpose                                                           |
|----------------------------------|-------------------------------------------------------------------|
| `qyl.web`                        | Composition root: REST API, SSE, frontend hosting, DI             |
| `qyl.collector`                  | OTLP ingest and telemetry processing only                         |
| `qyl.agents`                     | Loom, autofix, triage, summarization, and other AI-driven logic   |
| `qyl.mcp`                        | MCP tool surface as a library mounted by `qyl.web`                |
| `qyl.infrastructure`             | DuckDB and external integration implementations                   |
| `qyl.core`                       | Interfaces, DTOs, value objects, and query contracts              |
| `qyl.hosting`                    | App orchestration framework (`QylRunner`)                         |
| `qyl.instrumentation`            | .NET instrumentation library with OTel setup                      |
| `qyl.instrumentation.generators` | Roslyn source generator for GenAI/DB interceptors                 |
| `qyl.contracts`                  | Generated transport/shared types where still needed across layers |

## Quick Start

**Hosted**

<https://qyl-api-production.up.railway.app>

**Docker**

```bash
docker build -f src/qyl.collector/Dockerfile -t qyl .
docker run -d -p 5100:5100 -p 4317:4317 -v ~/.qyl:/data qyl
```

**From Source**

```bash
git clone https://github.com/ANcpLua/qyl.git
cd qyl
dotnet run --project src/qyl.web
```

## Instrument Your .NET App

Add the service defaults package to automatically instrument GenAI calls:

```csharp
// Program.cs
builder.AddQylServiceDefaults();
```

This auto-instruments:

- `IChatClient` calls (Microsoft.Extensions.AI)
- Token usage, latency, model info
- Full OTel 1.40 GenAI semantic conventions

For the full compile-time attribute mapping used by this instrumentation stack, see the
[Instrumentation Toolkit Reference](docs/instrumentation-toolkit.md).  
It defines the exact `[Traced]`/`[OTel]`/`[Meter]`/`[AgentTraced]` behavior and the corresponding generator pipelines.

Set the exporter endpoint:

```bash
export OTEL_EXPORTER_OTLP_ENDPOINT="http://localhost:5100"
```

## Use with Any OTel App

qyl accepts standard OTLP from any language/framework:

```bash
# Point any OpenTelemetry SDK at qyl
export OTEL_EXPORTER_OTLP_ENDPOINT="http://localhost:5100"
export OTEL_EXPORTER_OTLP_PROTOCOL="http/protobuf"
```

Supported protocols:

- OTLP/HTTP (port 5100)
- OTLP/gRPC (port 4317)

## Architecture

```text
                         +------------------+
                         |     qyl.web      |
                         | Composition root |
                         |  API + SSE + UI  |
                         +----+----+----+---+
                              |    |    |
              +---------------+    |    +----------------+
              |                    |                     |
              v                    v                     v
      +---------------+    +---------------+    +---------------+
      | qyl.collector |    |  qyl.agents   |    |    qyl.mcp    |
      |  OTLP ingest  |    | Loom + triage |    | Tool surface  |
      +-------+-------+    +-------+-------+    +-------+-------+
              \                    |                    /
               \                   |                   /
                \                  |                  /
                 v                 v                 v
                   +--------------------------------+
                   |            qyl.core            |
                   | Interfaces + DTOs + contracts  |
                   +----------------+---------------+
                                    |
                                    v
                        +--------------------------+
                        |    qyl.infrastructure    |
                        | DuckDB + GitHub impls    |
                        +------------+-------------+
                                     |
                                     v
                                  DuckDB
```

## Ports

| Port | Protocol | Purpose                        |
|------|----------|--------------------------------|
| 5100 | HTTP     | REST API, Dashboard, OTLP/HTTP |
| 4317 | gRPC     | OTLP/gRPC ingestion            |

## Environment Variables

| Variable                        | Default      | Purpose                    |
|---------------------------------|--------------|----------------------------|
| `QYL_PORT`                      | 5100         | HTTP API port              |
| `QYL_GRPC_PORT`                 | 4317         | gRPC OTLP port (0=disable) |
| `QYL_DATA_PATH`                 | ./qyl.duckdb | DuckDB file location       |
| `QYL_TOKEN`                     | (none)       | Auth token                 |
| `QYL_MAX_RETENTION_DAYS`        | 30           | Telemetry retention        |
| `QYL_MAX_SPAN_COUNT`            | 1000000      | Max spans before cleanup   |
| `QYL_MAX_LOG_COUNT`             | 500000       | Max logs before cleanup    |
| `QYL_CLEANUP_INTERVAL_SECONDS`  | 300          | Cleanup interval           |
| `QYL_OTLP_CORS_ALLOWED_ORIGINS` | *            | CORS origins (CSV)         |

## GenAI Telemetry

qyl captures OpenTelemetry 1.40 GenAI semantic conventions:

| Attribute                        | Description                       |
|----------------------------------|-----------------------------------|
| `gen_ai.provider.name`           | Provider (openai, anthropic, etc) |
| `gen_ai.request.model`           | Model name                        |
| `gen_ai.usage.input_tokens`      | Prompt tokens                     |
| `gen_ai.usage.output_tokens`     | Completion tokens                 |
| `gen_ai.response.finish_reasons` | Stop reason                       |

## TypeSpec-First Design

All types are defined in TypeSpec and generated downstream:

```
core/specs/*.tsp
       ↓ (tsp compile)
core/openapi/openapi.yaml
       ↓ (nuke Generate)
   ┌───┴───┬───────┬────────┐
   ↓       ↓       ↓        ↓
  C#    DuckDB    TS    JSON Schema
```

Never edit `*.g.cs` or `api.ts` — edit TypeSpec and regenerate.

## Idempotent Ingestion

Spans use `ON CONFLICT (span_id) DO UPDATE` — SDKs can safely retry on network errors without creating duplicates.
Mutable fields (tokens, status, cost) are updated; immutable fields (trace_id, name, start_time) are preserved.

## Development

```bash
# Full build (TypeSpec → Docker)
nuke Full

# Regenerate types from TypeSpec
nuke Generate --force-generate

# Run tests
dotnet test

# Dashboard dev server (hot reload)
cd src/qyl.dashboard && npm run dev

# Collector only
dotnet run --project src/qyl.collector
```

## Project Structure

```
core/                                    # TypeSpec schemas (source of truth)
eng/                                     # NUKE build system
src/
  qyl.web/                              # Composition root and frontend host
  qyl.collector/                        # OTLP ingest and telemetry processing
  qyl.agents/                           # Loom, autofix, triage, summarization
  qyl.mcp/                              # MCP surface library
  qyl.infrastructure/                   # DuckDB + external integration implementations
  qyl.core/                             # Interfaces, DTOs, value objects, contracts
  qyl.dashboard/                         # React 19 SPA
  qyl.hosting/                          # App orchestration (QylRunner)
  qyl.contracts/                         # Shared types (BCL-only)
  qyl.instrumentation/                   # OTel + health + resilience defaults
  qyl.instrumentation.generators/        # Roslyn auto-instrumentation
  qyl.collector.storage.generators/      # DuckDB source generators
  qyl.browser/                           # Browser OTLP SDK (TypeScript)
  qyl.watch/                             # Live terminal span viewer
  loom/                                  # Sentry Loom reference impl (read-only)
tests/                                   # xUnit v3 + MTP tests
```

## License

MIT
