# qyl

**Question Your Logs** — observe everything, judge nothing, document perfectly.

Landing page: https://ancplua.github.io/qyl/

## What qyl Does

|                 |                                                          |
|-----------------|----------------------------------------------------------|
| **Collects**    | OTLP receiver with idempotent ingestion (retry-safe)     |
| **Instruments** | Roslyn source generators for zero-config GenAI telemetry |
| **Visualizes**  | Real-time dashboard with SSE streaming                   |
| **Integrates**  | MCP server and GitHub Copilot for AI agent observability |

## Tech Stack

| Layer    | Technology                                    |
|----------|-----------------------------------------------|
| Runtime  | .NET 10.0 LTS, C# 14                          |
| Frontend | React 19, Vite 7, Tailwind CSS 4              |
| Storage  | DuckDB (columnar, upsert-based)               |
| Protocol | OpenTelemetry 1.39 GenAI Semantic Conventions |
| Schema   | TypeSpec → OpenAPI → C#/DuckDB/TypeScript     |

## Components

| Package                          | Purpose                                                     |
|----------------------------------|-------------------------------------------------------------|
| `qyl.collector`                  | OTLP receiver, DuckDB storage, REST API, embedded dashboard |
| `qyl.copilot`                    | GitHub Copilot integration with AG-UI tool rendering        |
| `qyl.hosting`                    | App orchestration framework (QylRunner)                     |
| `qyl.servicedefaults`            | .NET instrumentation library with OTel setup                |
| `qyl.servicedefaults.generator`  | Roslyn source generator for GenAI/DB interceptors           |
| `qyl.instrumentation.generators` | DuckDB insert + interceptor source generators               |
| `qyl.Analyzers`                  | Roslyn analyzers for OTel/GenAI best practices (15 rules)   |
| `qyl.mcp`                        | MCP server for AI agent integration                         |
| `qyl.protocol`                   | Shared types (BCL-only, no dependencies)                    |

## Quick Start

**Hosted**

https://qyl-api-production.up.railway.app

**Docker**

```bash
docker build -f src/qyl.collector/Dockerfile -t qyl .
docker run -d -p 5100:5100 -p 4317:4317 -v ~/.qyl:/data qyl
```

**From Source**

```bash
git clone https://github.com/ANcpLua/qyl.git
cd qyl
dotnet run --project src/qyl.collector
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
- Full OTel 1.39 GenAI semantic conventions

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

```
+------------------+                     +------------------+
|  Your .NET App   |                     |  Any OTel App    |
| (servicedefaults)|                     |  (Python, Go..)  |
+--------+---------+                     +--------+---------+
         |                                        |
         |               OTLP                     |
         +----------------+-----------------------+
                          v
            +----------------------------+
            |       qyl.hosting          |
            |      (QylRunner)           |
            +-------------+--------------+
                          | orchestrates
                          v
            +----------------------------+
            |       qyl.collector        |
            |        (ASP.NET)           |
            +---+------+------+-----+---+
                |      |      |     |
      +---------+  +---+---+  |  +--+--------+
      v            v       v  |  v            v
+----------+ +----------+ |  | +----------+ +----------------+
|  DuckDB  | | Dashboard| |  | |   MCP    | |  qyl.copilot   |
| (storage)| | (React)  | |  | | (agents) | | (GitHub Copilot)|
+----------+ +----------+ |  | +----------+ +-------+--------+
                           |  |                      |
                     +-----+--+-----+          SSE / AG-UI
                     | Insights     |                |
                     | Materializer |                v
                     +--------------+        GitHub Copilot
```

## AG-UI Tool Rendering

qyl.copilot exposes observability tools to GitHub Copilot via SSE streaming with AG-UI event conventions.

**Tools** (via `ObservabilityTools`):

| Tool                 | Description                                        |
|----------------------|----------------------------------------------------|
| `search_spans`       | Search spans by service name, status, time range   |
| `get_trace`          | Get all spans for a trace ID                       |
| `get_genai_stats`    | GenAI usage statistics (requests, tokens, costs)   |
| `search_logs`        | Search logs by severity, body text, time range     |
| `get_storage_stats`  | Storage statistics (span/log/session counts, size) |
| `list_sessions`      | List spans belonging to a session                  |
| `get_system_context` | Pre-computed system context (zero query cost)      |

Each tool is wrapped by `DelegatingAIFunction` which applies `CopilotMetrics` (counters, histograms) and
`CopilotInstrumentation` (OTel spans).

**Endpoints**:

| Method | Path                                   | Purpose                |
|--------|----------------------------------------|------------------------|
| POST   | `/api/v1/copilot/chat`                 | Chat (SSE streaming)   |
| GET    | `/api/v1/copilot/workflows`            | List workflows         |
| POST   | `/api/v1/copilot/workflows/{name}/run` | Execute workflow (SSE) |
| GET    | `/api/v1/copilot/status`               | Auth status            |
| GET    | `/api/v1/copilot/executions`           | Execution history      |
| GET    | `/api/v1/copilot/executions/{id}`      | Execution details      |

SSE events use AG-UI convention: `tool_call` and `tool_result` event names.

## Insights Materializer

Background service that pre-computes system context every 5 minutes (10-second warmup delay).

| Materializer           | Computes                                               |
|------------------------|--------------------------------------------------------|
| `TopologyMaterializer` | Service discovery, AI model usage                      |
| `ProfileMaterializer`  | Latency percentiles (P50/P95/P99), token costs, trends |
| `AlertsMaterializer`   | Error spikes, cost drift, slow operations              |

Results are stored in the `materialized_insights` table and served via `get_system_context` with zero query cost at read
time.

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

qyl captures OpenTelemetry 1.39 GenAI semantic conventions:

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
core/                                   # TypeSpec schemas (source of truth)
  qyl.watchdog/                         # Process anomaly detection daemon
eng/                                    # NUKE build system
examples/
  AgentsGateway/                        # Agent-based gateway example
  qyl.demo/                            # Minimal example with Copilot
src/
  qyl.collector/                        # Backend API service
  qyl.copilot/                          # GitHub Copilot integration (AG-UI)
  qyl.dashboard/                        # React frontend
  qyl.hosting/                          # App orchestration (QylRunner)
  qyl.mcp/                             # MCP server
  qyl.protocol/                         # Shared types (BCL-only)
  qyl.servicedefaults/                  # OTel instrumentation library
  qyl.servicedefaults.generator/        # Roslyn source generator
  qyl.instrumentation.generators/       # DuckDB insert + interceptor generators
  qyl.Analyzers/                        # Roslyn analyzers (QYL001-QYL015)
  qyl.Analyzers.CodeFixes/             # Code fix providers
tests/                                  # Test projects
```

## Analyzers

qyl includes 15 Roslyn analyzers for OpenTelemetry and GenAI best practices:

| ID         | Rule                          | Category      |
|------------|-------------------------------|---------------|
| QYL001-003 | OTel semantic conventions     | OpenTelemetry |
| QYL004-006 | GenAI span requirements       | GenAI         |
| QYL007-008 | Metric registration/naming    | Metrics       |
| QYL009-010 | ServiceDefaults config        | Configuration |
| QYL011-012 | Source generator requirements | Metrics       |
| QYL013     | Traced attribute validation   | OpenTelemetry |
| QYL014-015 | GenAI semconv/cardinality     | GenAI/Metrics |

## License

MIT
