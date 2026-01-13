# qyl.

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
# Start the collector (generates auth token on first run)
dotnet run --project src/qyl.collector

# Output:
#   qyl. AI Observability
#   ─────────────────────────────────────────
#   Dashboard:   http://localhost:5100
#   OTLP gRPC:   localhost:4317
#   OTLP HTTP:   localhost:5100/v1/traces
#   Token:       abc123...
```

Point any OpenTelemetry SDK at `localhost:4317` (gRPC) or `localhost:5100/v1/traces` (HTTP).

## Why qyl?

| Problem | qyl Solution |
|---------|--------------|
| Generic APM tools don't understand LLM calls | Purpose-built for `gen_ai.*` semantic conventions |
| Agents can't introspect their own behavior | MCP server lets Claude query its own traces |
| External databases add operational overhead | Embedded DuckDB — single binary, zero config |
| Token costs are invisible | First-class token tracking with cost attribution |

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
// Claude can call these tools to understand its own behavior
{
  "tools": [
    "qyl.search_agent_runs",   // Find runs by provider/model/error
    "qyl.get_agent_run",       // Get single run details
    "qyl.get_token_usage",     // Token usage by agent/model
    "qyl.list_errors",         // Recent errors with stack traces
    "qyl.get_latency_stats"    // P50/P95/P99 latency
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

## Architecture

| Component | Port | Purpose |
|-----------|------|---------|
| `qyl.collector` | 5100 (HTTP), 4317 (gRPC) | OTLP ingestion, REST API, SSE streaming |
| `qyl.dashboard` | 5173 | React SPA for visualization |
| `qyl.mcp` | stdio | MCP server for AI agent integration |
| `qyl.protocol` | — | Shared type contracts |

### Dependency Graph

```
dashboard ──HTTP──► collector ◄──HTTP── mcp
                        │
                        ▼
                    protocol (BCL only)
```

## Configuration

| Environment Variable | Default | Description |
|---------------------|---------|-------------|
| `QYL_PORT` | `5100` | HTTP/REST API port |
| `QYL_GRPC_PORT` | `4317` | OTLP gRPC port |
| `QYL_TOKEN` | (generated) | Dashboard auth token |
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
| `/api/v1/sessions` | GET | List sessions with optional filters |
| `/api/v1/sessions/{id}` | GET | Get session details |
| `/api/v1/sessions/{id}/spans` | GET | Get spans for session |
| `/api/v1/traces/{traceId}` | GET | Get trace tree |
| `/api/v1/live` | GET | SSE stream for real-time spans |

### MCP

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/mcp/manifest` | GET | MCP tool manifest |
| `/mcp/tools/call` | POST | Execute MCP tool |

## Tech Stack

- **Runtime**: .NET 10 / C# 14
- **Storage**: DuckDB (embedded columnar database)
- **Frontend**: React 19, Vite 6, Tailwind 4, TanStack Query 5
- **Protocols**: OpenTelemetry (OTLP), Model Context Protocol (MCP)
- **SDK**: ANcpLua.NET.Sdk 1.6.2
- **Testing**: xUnit v3 + Microsoft Testing Platform (MTP)

## Development

```bash
# Run collector
dotnet run --project src/qyl.collector

# Run dashboard (separate terminal)
cd src/qyl.dashboard && npm run dev

# Run tests
dotnet test

# Generate code from schema
nuke Generate --ForceGenerate
```

## Project Structure

```
qyl/
├── src/
│   ├── qyl.protocol/     # Shared types (BCL only, leaf dependency)
│   ├── qyl.collector/    # Backend: OTLP ingestion, DuckDB, REST API
│   ├── qyl.mcp/          # MCP server for AI agent integration
│   └── qyl.dashboard/    # React 19 SPA
├── tests/
│   └── qyl.collector.tests/
└── eng/
    └── build/Domain/CodeGen/  # Schema → code generators
```

## License

MIT
