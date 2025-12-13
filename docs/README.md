# qyl Documentation

> AI Observability Platform — .NET 10 · DuckDB · OTel 1.38 · MCP

## Quick Links

| Doc | Content |
|-----|---------|
| [ARCHITECTURE.md](ARCHITECTURE.md) | System design, components, data flow |
| [COLLECTOR.md](COLLECTOR.md) | Backend: OTLP, DuckDB, REST/SSE |
| [FRONTEND.md](FRONTEND.md) | Dashboard + MCP server |
| [BUILD.md](BUILD.md) | **Complete build guide** — Kiota, Docker, CI, Tests |

## What is qyl?

```
User Apps ──OTLP──► qyl.collector ──REST/SSE──► qyl.dashboard
                         │
                         └──HTTP──► qyl.mcp ──stdio──► Claude
```

Backend that:
1. Receives OpenTelemetry telemetry (OTLP)
2. Extracts `gen_ai.*` semantic convention attributes
3. Stores in DuckDB (embedded OLAP)
4. Exposes REST/SSE APIs for dashboards and AI agents

## Projects

| Project | Path | Purpose |
|---------|------|---------|
| qyl.protocol | `src/qyl.protocol` | Shared types (LEAF) |
| qyl.collector | `src/qyl.collector` | Backend |
| qyl.mcp | `src/qyl.mcp` | MCP server |
| qyl.dashboard | `src/qyl.dashboard` | React UI |

## Quick Start

```bash
# Build
nuke Compile

# Run backend
dotnet run --project src/qyl.collector

# Run frontend
npm run dev --prefix src/qyl.dashboard

# Docker (full stack)
docker compose -f eng/compose.yaml up -d
```

## Machine-Readable Spec

See [qyl-architecture.yaml](qyl-architecture.yaml) for complete machine-readable architecture specification.
