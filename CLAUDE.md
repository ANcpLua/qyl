# qyl. - AI Observability Platform

> Claude Code project context for qyl. development

## Project Overview

**qyl.** (pronounced "quill") is a lightweight AI observability platform for GenAI/LLM workloads. Single-binary deployment with embedded DuckDB, real-time SSE streaming, and a React dashboard.

## Architecture

```
qyl/
├── src/
│   ├── qyl.collector/          # .NET 10 Native AOT backend
│   │   ├── Auth/               # Token-based authentication
│   │   ├── ConsoleBridge/      # Frontend console.log→backend for AI debugging
│   │   ├── Ingestion/          # Schema normalization (v1.38 only)
│   │   ├── Mcp/                # MCP server for AI agent queries
│   │   ├── Query/              # Query handling
│   │   ├── Realtime/           # SSE pub/sub hub
│   │   ├── Storage/            # DuckDB persistence + Parquet cold tier
│   │   └── Program.cs          # Minimal API endpoints
│   │
│   ├── qyl.dashboard/          # React 19 + Tailwind v4 frontend
│   │   └── src/
│   │       ├── components/     # UI components (shadcn/ui pattern)
│   │       ├── hooks/          # use-telemetry, use-keyboard-shortcuts, use-theme
│   │       ├── lib/            # Utilities
│   │       ├── pages/          # 6 pages (GenAI, Logs, Metrics, Resources, Settings, Traces)
│   │       └── types/          # TypeScript definitions
│   │
│   ├── qyl.agents.telemetry/   # Telemetry library for agent instrumentation
│   ├── qyl.demo/               # Demo with v1.38 OpenTelemetry observability
│   ├── qyl.mcp.server/         # MCP server package
│   ├── qyl.providers.gemini/   # Gemini provider
│   ├── qyl.sdk.aspnetcore/     # ASP.NET Core SDK
│   ├── LegacySupport/          # Legacy compatibility utilities
│   └── Shared/                 # Shared code across projects
│
├── examples/
│   ├── qyl.analyzers.Sample/   # Analyzer sample
│   └── qyl.AspNetCore.Example/ # OpenTelemetry ASP.NET Core example
│
├── eng/                        # Build tooling, CLI, Analyzers
│   ├── qyl.cli/                # CLI for schema normalization
│   └── qyl.analyzers/          # Roslyn analyzers
├── schemas/                    # OpenTelemetry semantic convention schemas (v1.38)
├── tests/                      # Unit and integration tests
├── specification/              # OpenTelemetry specification reference
└── qyl.slnx                    # Solution file
```

## Key Technologies

### Backend (.NET 10)
- **Native AOT** - Single binary deployment
- **DuckDB** - Embedded columnar analytics, Parquet export
- **SSE** - Real-time streaming via `BoundedChannel`
- **MCP** - Model Context Protocol server for AI agents

### Frontend (React 19)
- **Tailwind v4** - `@theme` syntax with oklch colors
- **TanStack Query** - Data fetching with SSE integration
- **TanStack Virtual** - Virtualized logs
- **Recharts** - Metrics visualization
- **Radix UI** - Accessible component primitives

## Commands

```bash
# Backend
dotnet build src/qyl.collector
dotnet run --project src/qyl.collector

# Frontend
npm install --prefix src/qyl.dashboard
npm run dev --prefix src/qyl.dashboard
npm run build --prefix src/qyl.dashboard

# Run example
dotnet run --project examples/qyl.AspNetCore.Example/qyl.AspNetCore.Example.csproj
```

## Configuration

| Variable | Default | Description |
|----------|---------|-------------|
| `QYL_PORT` | 5100 | HTTP port |
| `QYL_TOKEN` | (generated) | Auth token |
| `QYL_DATA_PATH` | qyl.duckdb | Database path |

## Key Files

| File | Purpose |
|------|---------|
| `src/qyl.collector/Program.cs` | API endpoints |
| `src/qyl.collector/Storage/DuckDbStore.cs` | Storage + Parquet archival |
| `src/qyl.collector/Realtime/SseHub.cs` | Pub/sub with backpressure |
| `src/qyl.collector/Mcp/McpServer.cs` | MCP protocol implementation |
| `src/qyl.collector/Auth/TokenAuth.cs` | Authentication |
| `src/qyl.collector/Ingestion/SchemaNormalizer.cs` | Schema normalization |
| `src/qyl.dashboard/src/hooks/use-telemetry.ts` | Data hooks + utilities |
| `src/qyl.dashboard/src/pages/TracesPage.tsx` | Waterfall visualization |
| `src/qyl.dashboard/src/pages/LogsPage.tsx` | Virtualized log viewer |
| `src/qyl.dashboard/src/index.css` | Tailwind v4 theme |

## Dashboard Pages

- **GenAIPage** - GenAI/LLM specific telemetry
- **LogsPage** - Virtualized log viewer
- **MetricsPage** - Metrics visualization
- **ResourcesPage** - Resource explorer
- **SettingsPage** - Configuration
- **TracesPage** - Trace waterfall visualization

## API Endpoints

### Auth
- `POST /api/login` - Login with token
- `POST /api/logout` - Clear session
- `GET /api/auth/check` - Check authentication

### Query
- `GET /api/v1/sessions` - List sessions
- `GET /api/v1/sessions/{id}/spans` - Get session spans
- `GET /api/v1/traces/{id}` - Get trace by ID

### Realtime
- `GET /api/v1/live` - SSE stream (supports `?session=` filter)

### Console Bridge (Frontend Debugging)
- `POST /api/v1/console` - Receive frontend console logs
- `GET /api/v1/console` - Query logs (filter by level, session)
- `GET /api/v1/console/errors` - Get errors/warnings only
- `GET /api/v1/console/live` - SSE stream for console logs

### MCP
- `GET /mcp/manifest` - MCP tool definitions
- `POST /mcp/tools/call` - Execute MCP tool

### Ingestion
- `POST /api/v1/ingest` - qyl. native format
- `POST /v1/traces` - OTLP compatibility shim

### Health
- `GET /health` - Health check
- `GET /ready` - Readiness check

## MCP Tools

| Tool | Description |
|------|-------------|
| `get_sessions` | List sessions with stats |
| `get_trace` | Fetch trace by ID |
| `get_spans` | Query spans with filters |
| `get_genai_stats` | Token/cost aggregations |
| `search_errors` | Find error spans |
| `get_storage_stats` | Database statistics |
| `archive_old_data` | Export to Parquet |
| `get_console_logs` | Get frontend console logs |
| `get_console_errors` | Get frontend errors/warnings |

## Frontend Console Bridge

Drop-in JavaScript shim for frontend debugging:

```html
<script src="http://localhost:5100/qyl-console.js"></script>
```

Agents can see frontend `console.log/error` without browser MCP via:
- REST: `GET /api/v1/console?level=error`
- MCP: `get_console_errors` tool

## Design Documents

- `QYL_ARCHITECTURE.md` - Original specification
- `QYL_IMPLEMENTATION.md` - Detailed analysis & ADRs
- `spec-compliance-matrix.md` - Feature implementation status

## Schema Version

qyl uses **OpenTelemetry Semantic Conventions v1.38** exclusively. All schema normalization, GenAI telemetry, and trace processing follows v1.38 conventions. Pre-v1.38 compatibility code has been removed.

Reference: `schemas/1.38.0/` for v1.38 attribute definitions.

## Examples

### qyl.AspNetCore.Example

OpenTelemetry ASP.NET Core example demonstrating:
- Unified `AddOpenTelemetry()` fluent API
- All three signals (Tracing + Metrics + Logging)
- OTLP export to qyl.collector
- Manual span creation with `ActivitySource`
- Custom metrics with `Meter`
- Source-generated structured logging

```bash
# Run the example
dotnet run --project examples/qyl.AspNetCore.Example/qyl.AspNetCore.Example.csproj

# Hit the endpoint
curl http://localhost:5050/WeatherForecast
```
