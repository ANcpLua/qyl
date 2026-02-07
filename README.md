# qyl

**Question Your Logs** — OpenTelemetry collector and auto-instrumentation for AI workloads.

## What qyl Does

|                 |                                                          |
|-----------------|----------------------------------------------------------|
| **Collects**    | OTLP receiver with idempotent ingestion (retry-safe)     |
| **Instruments** | Roslyn source generators for zero-config GenAI telemetry |
| **Visualizes**  | Real-time dashboard with SSE streaming                   |
| **Integrates**  | MCP server for AI agent observability queries            |

## Tech Stack

| Layer    | Technology                                    |
|----------|-----------------------------------------------|
| Runtime  | .NET 10.0 LTS, C# 14                          |
| Frontend | React 19, Vite 7, Tailwind CSS 4              |
| Storage  | DuckDB (columnar, upsert-based)               |
| Protocol | OpenTelemetry 1.39 GenAI Semantic Conventions |
| Schema   | TypeSpec → OpenAPI → C#/DuckDB/TypeScript     |

## Components

| Package                         | Purpose                                                     |
|---------------------------------|-------------------------------------------------------------|
| `qyl.collector`                 | OTLP receiver, DuckDB storage, REST API, embedded dashboard |
| `qyl.servicedefaults`           | .NET instrumentation library with OTel setup                |
| `qyl.servicedefaults.generator` | Roslyn source generator for GenAI/DB interceptors           |
| `qyl.Analyzers`                 | Roslyn analyzers for OTel/GenAI best practices (15 rules)   |
| `qyl.mcp`                       | MCP server for AI agent integration                         |
| `qyl.protocol`                  | Shared types (BCL-only, no dependencies)                    |

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
┌─────────────────┐                    ┌─────────────────┐
│  Your .NET App  │                    │  Any OTel App   │
│  (servicedefaults)                   │  (Python, Go..) │
└────────┬────────┘                    └────────┬────────┘
         │                                      │
         │              OTLP                    │
         └──────────────┬───────────────────────┘
                        ▼
              ┌─────────────────┐
              │  qyl.collector  │
              │   (ASP.NET)     │
              └────────┬────────┘
                       │
         ┌─────────────┼─────────────┐
         ▼             ▼             ▼
   ┌──────────┐  ┌──────────┐  ┌──────────┐
   │  DuckDB  │  │ Dashboard│  │   MCP    │
   │ (storage)│  │ (React)  │  │ (agents) │
   └──────────┘  └──────────┘  └──────────┘
```

## Ports

| Port | Protocol | Purpose                        |
|------|----------|--------------------------------|
| 5100 | HTTP     | REST API, Dashboard, OTLP/HTTP |
| 4317 | gRPC     | OTLP/gRPC ingestion            |

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
core/           # TypeSpec schemas (source of truth)
eng/            # NUKE build system
src/
  qyl.collector/              # Backend API service
  qyl.dashboard/              # React frontend
  qyl.mcp/                    # MCP server
  qyl.protocol/               # Shared types (BCL-only)
  qyl.servicedefaults/        # OTel instrumentation library
  qyl.servicedefaults.generator/  # Roslyn source generator
  qyl.Analyzers/              # Roslyn analyzers (QYL001-QYL015)
  qyl.Analyzers.CodeFixes/    # Code fix providers
tests/          # Test projects
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
