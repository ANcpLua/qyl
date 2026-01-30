# qyl

**Question Your Logs** — OpenTelemetry observability for AI workloads.

Collects and visualizes telemetry from AI systems: token usage, latency, errors, and costs across LLM operations.

## Features

- **OTLP Ingestion** — Native OpenTelemetry protocol support (gRPC and HTTP)
- **GenAI Semantic Conventions** — Full OTel 1.39 gen_ai.* attribute support
- **Real-time Dashboard** — Live streaming of traces, spans, and metrics
- **DuckDB Storage** — Fast columnar analytics on telemetry data
- **Zero Config** — Works out of the box with any OTel-instrumented app

## Quick Start

**Railway (hosted)**

Visit: https://qyl-api-production.up.railway.app

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

Dashboard: http://localhost:5100

## Send Telemetry

Configure your OpenTelemetry SDK to export to qyl:

```bash
export OTEL_EXPORTER_OTLP_ENDPOINT="http://localhost:5100"
export OTEL_EXPORTER_OTLP_PROTOCOL="http/protobuf"
```

Or for gRPC:

```bash
export OTEL_EXPORTER_OTLP_ENDPOINT="http://localhost:4317"
```

## Architecture

```
┌─────────────────┐     OTLP      ┌─────────────────┐
│  Your AI App    │──────────────▶│  qyl.collector  │
│  (OTel SDK)     │   gRPC/HTTP   │   (ASP.NET)     │
└─────────────────┘               └────────┬────────┘
                                           │
                                           ▼
┌─────────────────┐               ┌─────────────────┐
│  qyl.dashboard  │◀──────────────│     DuckDB      │
│   (React 19)    │     REST      │  (columnar)     │
└─────────────────┘               └─────────────────┘
```

## Ports

| Port | Protocol | Purpose |
|------|----------|---------|
| 5100 | HTTP | REST API, Dashboard, OTLP/HTTP |
| 4317 | gRPC | OTLP/gRPC ingestion |

## Tech Stack

- .NET 10, C# 14
- React 19, Vite, Tailwind CSS
- DuckDB (columnar storage)
- OpenTelemetry Semantic Conventions 1.39

## Development

```bash
# Build everything
nuke Full

# Run tests
dotnet test

# Dashboard dev server
cd src/qyl.dashboard && npm run dev
```

## License

MIT
