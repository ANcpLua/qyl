---
name: setup
description: Set up qyl development environment and project configuration. Use when the user says "set up qyl", "configure qyl", "initialize qyl", or "qyl init".
---

# qyl Setup

## Install Prerequisites

**.NET 10.0 SDK:**
```bash
# macOS
brew install dotnet-sdk

# Verify
dotnet --version
```

**Node.js (for dashboard):**
```bash
node --version
npm --version
```

## Restore & Build

```bash
dotnet restore
dotnet build
```

## Environment Variables

| Variable | Default | Purpose |
|----------|---------|---------|
| `QYL_PORT` | 5100 | HTTP API port |
| `QYL_GRPC_PORT` | 4317 | gRPC OTLP port (0 to disable) |
| `QYL_DATA_PATH` | ./qyl.duckdb | DuckDB file location |
| `QYL_TOKEN` | (none) | Auth token (disabled if unset) |
| `QYL_MAX_RETENTION_DAYS` | 30 | Data retention period |

## Run the Collector

```bash
dotnet run --project src/qyl.collector
```

## Run the Dashboard (dev)

```bash
cd src/qyl.dashboard
npm install
npm run dev
```

## Verify Setup

```bash
# Health check
curl http://localhost:5100/health

# Send test OTLP data
curl -X POST http://localhost:5100/v1/traces \
  -H "Content-Type: application/json" \
  -d '{"resourceSpans":[]}'
```
