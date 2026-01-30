# qyl

**Question Your Logs** — OpenTelemetry collector and instrumentation for AI workloads.

## What qyl Does

**Collects** — Receives OpenTelemetry data from any OTLP-compatible source
**Instruments** — Provides .NET source generators that auto-instrument GenAI calls
**Visualizes** — Real-time dashboard for traces, spans, metrics, and AI-specific data

## Components

| Package | Purpose |
|---------|---------|
| `qyl.collector` | OTLP receiver, DuckDB storage, REST API, dashboard |
| `qyl.servicedefaults` | .NET instrumentation with GenAI interceptor generator |
| `qyl.mcp` | MCP server for AI agent integration |

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

| Port | Protocol | Purpose |
|------|----------|---------|
| 5100 | HTTP | REST API, Dashboard, OTLP/HTTP |
| 4317 | gRPC | OTLP/gRPC ingestion |

## GenAI Telemetry

qyl captures OpenTelemetry 1.39 GenAI semantic conventions:

| Attribute | Description |
|-----------|-------------|
| `gen_ai.system` | Provider (openai, anthropic, etc) |
| `gen_ai.request.model` | Model name |
| `gen_ai.usage.input_tokens` | Prompt tokens |
| `gen_ai.usage.output_tokens` | Completion tokens |
| `gen_ai.response.finish_reasons` | Stop reason |

## Development

```bash
nuke Full          # Build everything
dotnet test        # Run tests
cd src/qyl.dashboard && npm run dev  # Dashboard dev
```

## License

MIT
