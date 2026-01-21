# qyl

**Question Your Logs** — AI Observability Platform

Observe everything. Judge nothing. Document perfectly.

## What is qyl?

qyl collects and visualizes telemetry from AI agent systems using OpenTelemetry standards. Track token usage, latency, errors, and costs across your AI workloads.

## Quick Start

### Docker (Recommended)

```bash
docker run -d \
  -p 5100:5100 \
  -p 4317:4317 \
  -v ~/.qyl:/data \
  ghcr.io/ancplua/qyl:latest
```

Then open http://localhost:5100 in your browser.

### .NET Global Tool

```bash
dotnet tool install -g qyl
qyl start
```

## Sending Telemetry

Configure your OpenTelemetry SDK to export traces:

```yaml
# OTLP HTTP
endpoint: http://localhost:5100/v1/traces

# OTLP gRPC
endpoint: http://localhost:4317
```

### Python Example

```python
from opentelemetry import trace
from opentelemetry.exporter.otlp.proto.http.trace_exporter import OTLPSpanExporter
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import BatchSpanProcessor

provider = TracerProvider()
provider.add_span_processor(
    BatchSpanProcessor(OTLPSpanExporter(endpoint="http://localhost:5100/v1/traces"))
)
trace.set_tracer_provider(provider)
```

### .NET Example

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddOtlpExporter(o => o.Endpoint = new Uri("http://localhost:4317")));
```

## Features

- **OTLP Ingestion** — gRPC (4317) and HTTP (5100) endpoints
- **Real-time Dashboard** — React-based UI with live updates via SSE
- **GenAI Semantics** — Native support for OTel 1.39 gen_ai.* attributes
- **Token Tracking** — Input/output tokens, cost estimation
- **Session Grouping** — Organize spans by session_id
- **DuckDB Storage** — Fast analytical queries on telemetry data
- **MCP Integration** — Query telemetry from AI assistants

## Architecture

```
┌─────────────────┐     OTLP      ┌─────────────────┐
│  Your AI App    │──────────────▶│   qyl.collector │
│  (any language) │  HTTP/gRPC    │   (port 5100)   │
└─────────────────┘               └────────┬────────┘
                                           │
┌─────────────────┐     HTTP      ┌────────▼────────┐
│   qyl.mcp       │◀─────────────▶│    DuckDB       │
│  (AI assistant) │  REST API     │   (storage)     │
└─────────────────┘               └────────┬────────┘
                                           │
┌─────────────────┐     SSE       ┌────────▼────────┐
│   Browser       │◀─────────────▶│   Dashboard     │
│   (user)        │  /api/v1/live │   (React SPA)   │
└─────────────────┘               └─────────────────┘
```

## Configuration

| Environment Variable | Default | Description |
|---------------------|---------|-------------|
| `QYL_PORT` | 5100 | HTTP API port |
| `QYL_GRPC_PORT` | 4317 | OTLP gRPC port |
| `QYL_DATA_PATH` | `./qyl.duckdb` | DuckDB file path |

## Development

```bash
# Run collector
dotnet run --project src/qyl.collector

# Run dashboard (dev mode)
cd src/qyl.dashboard && npm run dev

# Run tests
dotnet test

# Full build with NUKE
./eng/build.ps1
```

## API Endpoints

| Endpoint | Description |
|----------|-------------|
| `POST /v1/traces` | OTLP HTTP trace ingestion |
| `GET /api/v1/sessions` | List sessions |
| `GET /api/v1/sessions/{id}` | Get session details |
| `GET /api/v1/traces/{id}` | Get trace details |
| `GET /api/v1/live` | SSE stream (real-time) |
| `GET /api/v1/stats/tokens` | Token usage statistics |

## MCP Integration

Add qyl to Claude Desktop:

```json
{
  "mcpServers": {
    "qyl": {
      "command": "dotnet",
      "args": ["run", "--project", "path/to/src/qyl.mcp"],
      "env": {
        "QYL_COLLECTOR_URL": "http://localhost:5100"
      }
    }
  }
}
```

Available tools: `qyl.list_sessions`, `qyl.get_session_transcript`, `qyl.search_agent_runs`, `qyl.get_token_usage`, `qyl.list_errors`

## Tech Stack

- **.NET 10** — Collector, MCP server, protocol
- **React 19** — Dashboard UI
- **DuckDB** — Analytical storage
- **OpenTelemetry 1.15.0** — Semantic conventions (1.39)
- **TypeSpec** — Schema generation
- **xUnit v3 3.2.2** — Testing framework

### ANcpLua Ecosystem Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| ANcpLua.NET.Sdk | 1.6.21 | MSBuild SDK for .NET projects |
| ANcpLua.Analyzers | 1.9.0 | Code quality analyzers |
| ANcpLua.Roslyn.Utilities | 1.16.0 | Roslyn utilities |
| ANcpLua.Roslyn.Utilities.Testing | 1.16.0 | Testing utilities |

## License

MIT
