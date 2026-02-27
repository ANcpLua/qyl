# qyl - AI Observability Platform

Polyglot OTLP collector — like Grafana/Jaeger, but for AI. Docker image IS the product.

## Architecture

```text
              +------------------+
              |   qyl.dashboard  |
              |    (React 19)    |
              +--------+---------+
                       | HTTP
                       v
+----------+  +------------------+  +------+
| qyl.mcp  |->|  qyl.collector   |<-| OTLP |
| (stdio)  |  |  (ASP.NET Core)  |  |Clients|
+----------+  +--------+---------+  +------+
                       |
                       v
              +------------------+
              |     DuckDB       |
              +------------------+
```

## Quick Start

```bash
dotnet run --project src/qyl.collector          # Backend (5100/4317)
cd src/qyl.dashboard && npm run dev             # Frontend (5173)
nuke Full                                        # Full build
nuke Generate --force-generate                   # Regenerate types
dotnet test                                      # Run tests
```

## Project Structure

| Directory                             | Purpose                                        |
|---------------------------------------|------------------------------------------------|
| `core/`                               | TypeSpec schemas (source of truth)             |
| `eng/`                                | NUKE build system                              |
| `src/qyl.collector/`                  | Backend API + gRPC + storage                   |
| `src/qyl.copilot/`                    | GitHub Copilot integration                     |
| `src/qyl.dashboard/`                  | React 19 SPA                                   |
| `src/qyl.hosting/`                    | App orchestration framework                    |
| `src/qyl.mcp/`                        | MCP server for AI agents                       |
| `src/qyl.protocol/`                   | Shared types (BCL-only)                        |
| `src/qyl.servicedefaults/`            | OTel + health + resilience defaults            |
| `src/qyl.servicedefaults.generator/`  | Roslyn auto-instrumentation                    |
| `src/qyl.instrumentation.generators/` | Telemetry source generators                    |
| `src/qyl.browser/`                    | Browser OTLP SDK (TypeScript, ESM + IIFE)      |
| `src/qyl.watch/`                      | Live terminal span viewer (dotnet tool)        |
| `tests/`                              | xUnit v3 + MTP tests                           |

## Tech Stack

| Layer    | Technology                           |
|----------|--------------------------------------|
| Runtime  | .NET 10.0 LTS, C# 14                 |
| Frontend | React 19, Vite 7, Tailwind CSS 4     |
| Storage  | DuckDB (columnar, glibc required)    |
| Protocol | OTel Semantic Conventions 1.40       |
| Testing  | xUnit v3, Microsoft Testing Platform |

## TypeSpec-First Design

All types in `core/specs/` — never edit `*.g.cs` or `api.ts`.

```text
core/specs/*.tsp -> openapi.yaml -> C# | DuckDB | TypeScript | JSON
```

## Allowed Dependencies

```yaml
allowed:
  collector -> protocol (ProjectReference)
  mcp -> protocol (ProjectReference)
  dashboard -> collector (HTTP runtime)
  mcp -> collector (HTTP runtime)
forbidden:
  mcp -> collector (ProjectReference)    # must use HTTP
  protocol -> any-package                # must stay BCL-only
```

## Ports

| Port | Protocol | Purpose                  |
|------|----------|--------------------------|
| 5100 | HTTP     | REST API, SSE, Dashboard |
| 4317 | gRPC     | OTLP traces/logs/metrics |
| 5173 | HTTP     | Dashboard dev server     |

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

## Documentation Map

Each component has its own `CLAUDE.md` with component-specific patterns.

## Observability Enhancements v1.0

- Build failures are captured via hook-based binlog collection and stored in collector DuckDB (`build_failures` table).
- Source locations are attached to structured logs (`source_file`, `source_line`, `source_column`, `source_method`) when
  available.
- Runtime controls:
  - `QYL_BUILD_FAILURE_CAPTURE_ENABLED` (default: `true`)
  - `QYL_MAX_BUILD_FAILURES` (default: `10`)
  - `QYL_BINLOG_DIR` (default: `.qyl/binlogs`)
