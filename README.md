# qyl.

[![CI](https://github.com/ancplua/qyl/actions/workflows/ci.yml/badge.svg)](https://github.com/ancplua/qyl/actions/workflows/ci.yml)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![OpenTelemetry](https://img.shields.io/badge/OpenTelemetry-1.38-blueviolet)](https://opentelemetry.io/)

**AI Observability for AI Agents** — An OpenTelemetry backend purpose-built for `gen_ai.*` semantic conventions.

qyl enables AI agents to observe themselves. Point your OTLP exporter at qyl, and your agents can query their own traces, token usage, and performance through the Model Context Protocol.

```
Your App ──OTLP──► qyl.collector ──DuckDB──► Storage
                        │                       │
                        │                       └──► REST/SSE ──► Dashboard
                        └──► REST ──► qyl.mcp ──stdio──► Claude
```

## Quick Start

```bash
# Start the collector
dotnet run --project src/qyl.collector

# Output:
#   qyl. AI Observability
#   ─────────────────────────────────────────
#   Dashboard:   http://localhost:5100
#   OTLP gRPC:   localhost:4317
#   OTLP HTTP:   localhost:5100/v1/traces
```

Point any OpenTelemetry SDK at `localhost:4317` (gRPC) or `localhost:5100/v1/traces` (HTTP).

## Why qyl?

| Problem | qyl Solution |
|---------|--------------|
| Generic APM tools don't understand LLM calls | Purpose-built for `gen_ai.*` semantic conventions |
| Agents can't introspect their own behavior | MCP server lets Claude query its own traces |
| External databases add operational overhead | Embedded DuckDB — single binary, zero config |
| Token costs are invisible | First-class token tracking with cost attribution |

## Architecture

```
schema/main.tsp (TypeSpec)
     │
     └─► openapi.yaml
              │
              ├─► C# types (protocol/*.g.cs)
              ├─► DuckDB schema (collector/Storage/*.g.cs)
              └─► TypeScript (dashboard/src/types/api.ts)
```

### Components

| Component | Port | Purpose |
|-----------|------|---------|
| `qyl.collector` | 5100 (HTTP), 4317 (gRPC) | OTLP ingestion, REST API, SSE streaming |
| `qyl.dashboard` | 5173 | React 19 SPA for visualization |
| `qyl.mcp` | stdio | MCP server for AI agent integration |
| `qyl.protocol` | — | Shared type contracts (BCL only) |

### Dependency Graph

```
dashboard ──HTTP──► collector ◄──HTTP── mcp
                        │
                        ▼
                    protocol (BCL only)
```

## Schema-First Development

TypeSpec is the **single source of truth**. All types flow from `schema/main.tsp`:

```bash
# Generate everything from TypeSpec
./eng/build.sh Generate

# Or with force overwrite
./eng/build.sh Generate --igenerate-force-generate true
```

This generates:
- `schema/generated/openapi.yaml` — OpenAPI 3.1 spec
- `src/qyl.protocol/Primitives/Scalars.g.cs` — Strongly-typed IDs
- `src/qyl.protocol/Enums/Enums.g.cs` — OTel enums
- `src/qyl.protocol/Models/*.g.cs` — Domain models
- `src/qyl.collector/Storage/DuckDbSchema.g.cs` — DDL
- `src/qyl.dashboard/src/types/api.ts` — TypeScript types

**Never edit `*.g.cs` or generated TypeScript files manually.**

## Features

### GenAI-Native Telemetry

qyl understands OpenTelemetry's [gen_ai semantic conventions](https://opentelemetry.io/docs/specs/semconv/gen-ai/) (v1.38):

- **Token usage**: `gen_ai.usage.input_tokens`, `gen_ai.usage.output_tokens`
- **Model attribution**: `gen_ai.request.model`, `gen_ai.response.model`
- **Provider detection**: Auto-detects OpenAI, Anthropic, Gemini, Bedrock, Azure OpenAI
- **Tool calls**: Tracks `gen_ai.tool.name`, `gen_ai.tool.call_id`
- **Session grouping**: Links spans by `gen_ai.conversation_id` or `qyl.session.id`

### MCP Integration

qyl exposes telemetry to AI agents via the [Model Context Protocol](https://modelcontextprotocol.io/):

```json
{
  "tools": [
    "qyl.search_agent_runs",
    "qyl.get_agent_run",
    "qyl.get_token_usage",
    "qyl.list_errors",
    "qyl.get_latency_stats"
  ]
}
```

### Real-Time Dashboard

- **Session timeline**: View conversation flows across traces
- **Span waterfall**: Visualize LLM call hierarchies
- **Token analytics**: Track usage and costs over time
- **Live streaming**: SSE-powered real-time updates

### Zero-Config Storage

DuckDB provides:
- **Embedded**: No external database to manage
- **Columnar**: Fast analytical queries on telemetry data
- **Portable**: Single file, easy backup and migration

## Development

### Prerequisites

- .NET 10 SDK
- Node.js 22+
- Docker (for integration tests)

### Commands

```bash
# Build everything
./eng/build.sh Compile

# Run all tests (140 tests)
./eng/build.sh Test

# Run with coverage
./eng/build.sh Coverage

# Start development environment
./eng/build.sh Dev

# Full CI pipeline
./eng/build.sh Full
```

### Running Locally

```bash
# Terminal 1: Collector
dotnet run --project src/qyl.collector

# Terminal 2: Dashboard
cd src/qyl.dashboard && npm run dev

# Terminal 3: Send test traces
# Point any OTLP exporter at localhost:4317
```

### Code Generation

```bash
# Compile TypeSpec → OpenAPI
./eng/build.sh TypeSpecCompile

# Generate C#/DuckDB from OpenAPI
./eng/build.sh Generate --igenerate-force-generate true

# Generate TypeScript types
cd src/qyl.dashboard && npm run generate:ts
```

## Configuration

| Environment Variable | Default | Description |
|---------------------|---------|-------------|
| `QYL_PORT` | `5100` | HTTP/REST API port |
| `QYL_GRPC_PORT` | `4317` | OTLP gRPC port |
| `QYL_DATA_PATH` | `qyl.duckdb` | DuckDB file location |

## API Endpoints

### OTLP Ingestion

| Endpoint | Method | Description |
|----------|--------|-------------|
| `localhost:4317` | gRPC | OTLP gRPC (TraceService) |
| `/v1/traces` | POST | OTLP HTTP/JSON |

### REST API

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/v1/sessions` | GET | List sessions with filters |
| `/api/v1/sessions/{id}` | GET | Get session details |
| `/api/v1/sessions/{id}/spans` | GET | Get spans for session |
| `/api/v1/traces/{traceId}` | GET | Get trace tree |
| `/api/v1/live` | GET | SSE stream for real-time spans |

### MCP

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/mcp/manifest` | GET | MCP tool manifest |
| `/mcp/tools/call` | POST | Execute MCP tool |

## Project Structure

```
qyl/
├── schema/                      # TypeSpec (SSOT)
│   ├── main.tsp                 # Entry point
│   ├── primitives.tsp           # TraceId, SpanId, etc.
│   ├── enums.tsp                # SpanKind, StatusCode
│   ├── models.tsp               # SpanRecord, SessionSummary
│   ├── api.tsp                  # REST API schemas
│   └── generated/               # Build output
│       └── openapi.yaml
├── src/
│   ├── qyl.protocol/            # Shared types (BCL only)
│   │   ├── Primitives/*.g.cs
│   │   ├── Enums/*.g.cs
│   │   └── Models/*.g.cs
│   ├── qyl.collector/           # Backend
│   │   ├── Storage/DuckDbSchema.g.cs
│   │   └── Ingestion/
│   ├── qyl.mcp/                 # MCP server
│   └── qyl.dashboard/           # React 19 SPA
│       └── src/types/api.ts
├── tests/
│   └── qyl.collector.tests/
└── eng/
    └── build/                   # NUKE build system
        └── Domain/CodeGen/      # OpenAPI generators
```

## Tech Stack

| Layer | Technology |
|-------|------------|
| Runtime | .NET 10 / C# 14 |
| Schema | TypeSpec → OpenAPI 3.1 |
| Storage | DuckDB (embedded columnar) |
| Frontend | React 19, Vite 6, Tailwind 4, TanStack Query 5 |
| Protocols | OpenTelemetry (OTLP), Model Context Protocol (MCP) |
| Build | NUKE |
| Testing | xUnit v3 + Microsoft Testing Platform |
| SDK | ANcpLua.NET.Sdk 1.6.3 |

## License

MIT
