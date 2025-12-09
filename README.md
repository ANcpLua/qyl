# qyl.

**Lightweight AI Observability for GenAI/LLM Workloads**

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![React](https://img.shields.io/badge/React-19-61DAFB)](https://react.dev/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

---

qyl. (pronounced "quill") is a single-binary observability platform designed specifically for AI applications. No Redis, PostgreSQL, or Kafka required - just DuckDB embedded in a ~25MB Docker image.

## Features

- **GenAI-Native** - First-class OpenTelemetry semantic conventions 1.38 (gen_ai.*)
- **Single Binary** - Native AOT compilation, ~500ms startup
- **Embedded Storage** - DuckDB columnar database with Parquet cold tier
- **Real-time** - Server-Sent Events (SSE) for live telemetry
- **Cost Tracking** - Token counting and cost estimation built-in
- **MCP Server** - Query telemetry from AI agents via Model Context Protocol
- **Beautiful Dashboard** - React 19 + Tailwind v4 with keyboard shortcuts

## Quick Start

### Docker (Recommended)

```bash
docker compose up
```

Open http://localhost:5100 - the login token is printed in the console.

### Manual

```bash
# Backend
dotnet run --project src/qyl.collector

# Frontend (in another terminal)
cd src/qyl.dashboard
npm install
npm run dev
```

## Screenshots

| Resources | Traces | Logs |
|-----------|--------|------|
| Grid/List/Graph views | Waterfall visualization | Virtualized 10k+ rows |

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     qyl.dashboard                            │
│              React 19 + Tailwind v4 + TanStack               │
└─────────────────────────┬───────────────────────────────────┘
                          │ HTTP/SSE
┌─────────────────────────▼───────────────────────────────────┐
│                     qyl.collector                            │
│                   .NET 10 Native AOT                         │
├─────────────────────────────────────────────────────────────┤
│  Auth   │  Ingestion  │  Query  │  Realtime  │    MCP       │
│ (Token) │ (Normalize) │  (API)  │   (SSE)    │  (AI Tools)  │
└─────────────────────────┬───────────────────────────────────┘
                          │
┌─────────────────────────▼───────────────────────────────────┐
│                      DuckDB                                  │
│           Embedded Columnar + Parquet Export                 │
└─────────────────────────────────────────────────────────────┘
```

## Configuration

| Environment Variable | Default | Description |
|---------------------|---------|-------------|
| `QYL_PORT` | 5100 | HTTP port |
| `QYL_TOKEN` | (auto-generated) | Authentication token |
| `QYL_DATA_PATH` | qyl.duckdb | Database file path |

## API

### Query Endpoints

```bash
# List sessions
curl http://localhost:5100/api/v1/sessions

# Get trace
curl http://localhost:5100/api/v1/traces/{traceId}

# Get session spans
curl http://localhost:5100/api/v1/sessions/{sessionId}/spans
```

### Real-time Streaming

```bash
# SSE stream (all sessions)
curl -N http://localhost:5100/api/v1/live

# SSE stream (filtered)
curl -N http://localhost:5100/api/v1/live?session=abc123
```

### MCP Tools

```bash
# Get available tools
curl http://localhost:5100/mcp/manifest

# Call a tool
curl -X POST http://localhost:5100/mcp/tools/call \
  -H "Content-Type: application/json" \
  -d '{"name": "get_storage_stats"}'
```

Available MCP tools:
- `get_sessions` - List sessions with stats
- `get_trace` - Fetch trace by ID
- `get_spans` - Query spans with filters
- `get_genai_stats` - Token/cost aggregations
- `search_errors` - Find error spans
- `get_storage_stats` - Database statistics
- `archive_old_data` - Export to Parquet

### Ingestion

```bash
# qyl. native format
curl -X POST http://localhost:5100/api/v1/ingest \
  -H "Content-Type: application/json" \
  -d '{"spans": [...]}'

# OTLP compatibility
curl -X POST http://localhost:5100/v1/traces \
  -H "Content-Type: application/json" \
  -d '{"resourceSpans": [...]}'
```

## Dashboard Pages

| Page | Description | Keyboard |
|------|-------------|----------|
| Resources | Service overview, grid/list/graph views | `G` |
| Traces | Waterfall visualization, span details | `T` |
| Logs | Virtualized log viewer, level filtering | `L` |
| Metrics | Request rate, latency, error rate charts | `M` |
| GenAI | LLM calls, token usage, cost breakdown | `A` |
| Settings | Theme, shortcuts, data management | `,` |

Press `?` anywhere to see all keyboard shortcuts.

## Tech Stack

### Backend
- .NET 10 Native AOT
- DuckDB (embedded columnar)
- Server-Sent Events

### Frontend
- React 19
- Tailwind CSS v4
- TanStack Query + Virtual
- Recharts
- Radix UI

## Development

```bash
# Run tests
dotnet test
npm test --prefix src/qyl.dashboard

# Build for production
dotnet publish -c Release
npm run build --prefix src/qyl.dashboard

# Build Docker image
docker build -t qyl .
```

## Documentation

- [CLAUDE.md](CLAUDE.md) - Project context for Claude Code
- [QYL_ARCHITECTURE.md](QYL_ARCHITECTURE.md) - Original specification
- [QYL_IMPLEMENTATION.md](QYL_IMPLEMENTATION.md) - Detailed implementation analysis

## License

MIT
