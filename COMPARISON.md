# qyl vs Aspire Dashboard

**tl;dr:** qyl is Aspire dashboard with persistent storage, zero-config GenAI instrumentation, and no orchestrator required.

## Feature Comparison

| Feature | qyl | Aspire 13.1 |
|---------|-----|-------------|
| **OTLP Ingestion** | gRPC (4317) + HTTP (5100) | gRPC + HTTP |
| **Storage** | DuckDB (persistent, columnar) | In-memory only |
| **Dashboard** | React 19 embedded | Blazor |
| **Real-time Streaming** | SSE (`/api/v1/live`) | SignalR |
| **Traces/Spans** | Full OTel 1.39 | Full OTel |
| **GenAI Telemetry** | Native + auto-instrumentation | Visualizer (13.1) |
| **GenAI Auto-Instrument** | Source generator (zero-config) | Manual setup |
| **Idempotent Ingestion** | Upsert on span_id | No |
| **MCP Server** | AI agent queries | `aspire mcp init` (13.1) |
| **Docker** | Single container | Single container |
| **Standalone Mode** | Always standalone | Optional |
| **Restart Persistence** | Yes (DuckDB) | No (lost on restart) |
| **TLS Termination** | Manual | Built-in (13.1) |
| **GitHub Copilot** | No | IDE integration (13.1) |
| **Resource Graph** | No | Visual deps (13.1) |

## What Aspire 13.1 Has That qyl Doesn't (Yet)

| Feature | Notes |
|---------|-------|
| **Resources Page** | No AppHost concept in qyl |
| **Console Logs** | qyl has structured logs only |
| **GitHub Copilot Integration** | IDE integration required |
| **Start/Stop Resources** | Not relevant (standalone) |
| **Auth (OIDC, BrowserToken)** | qyl currently unsecured |
| **Resource Graph View** | Visual dependency graph |
| **Keyboard Shortcuts** | Dashboard navigation |
| **Parameters Tab** | Dedicated config params view (13.1) |
| **GenAI Visualizer** | Tool definitions, video/audio preview (13.1) |
| **MCP Init CLI** | `aspire mcp init` for agent config (13.1) |
| **TLS Termination** | Built-in HTTPS cert management (13.1) |
| **Azure Functions** | KEDA auto-scaling (13.1) |
| **Container Registry Resource** | Push pipeline (13.1, experimental) |
| **JS/TS Starter Template** | `aspire-ts-cs-starter` (13.1) |

## qyl's Unique Value

### 1. Persistent Storage

```
Aspire: Telemetry in memory → Lost on restart
qyl:    Telemetry in DuckDB → Survives restarts, queryable
```

DuckDB is columnar — aggregations over millions of spans are fast.

### 2. Idempotent Ingestion

```sql
INSERT INTO spans (...) VALUES (...)
ON CONFLICT (span_id) DO UPDATE SET
    end_time_unix_nano = EXCLUDED.end_time_unix_nano,
    status_code = EXCLUDED.status_code,
    gen_ai_input_tokens = EXCLUDED.gen_ai_input_tokens,
    ...
```

SDKs can retry on network errors without creating duplicates. Mutable fields update; immutable fields preserved.

### 3. Zero-Config GenAI Instrumentation

```csharp
// One line in Program.cs
builder.AddQylServiceDefaults();

// That's it. IChatClient calls are now traced with:
// - gen_ai.system
// - gen_ai.request.model
// - gen_ai.usage.input_tokens
// - gen_ai.usage.output_tokens
// - gen_ai.response.finish_reasons
```

Roslyn source generator intercepts at compile time. No reflection, no runtime overhead.

**Aspire 13.1 GenAI Visualizer** requires manual setup:
```csharp
// Must configure each AI client manually
builder.Services.AddChatClient(...)
    .UseOpenTelemetry();  // Still needs explicit setup

// Plus environment variable for content capture
OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT=true
```

qyl's source generator does this automatically for `IChatClient`, OpenAI, Anthropic, Ollama, and Azure AI.

### 4. TypeSpec-First Schema

```
core/specs/*.tsp           (source of truth)
       ↓
core/openapi/openapi.yaml  (generated)
       ↓
   ┌───┴───┬───────┬────────┐
   ↓       ↓       ↓        ↓
  C#    DuckDB    TS    JSON Schema
```

Change the TypeSpec, regenerate everything. No drift between API, storage, and frontend types.

### 5. Works With Any Language

```bash
# Point any OpenTelemetry SDK at qyl
export OTEL_EXPORTER_OTLP_ENDPOINT="http://localhost:5100"
export OTEL_EXPORTER_OTLP_PROTOCOL="http/protobuf"

# Python, Go, Java, Node.js — all work
```

qyl is a standard OTLP collector. The .NET source generator is a bonus, not a requirement.

## Quick Start Comparison

### Aspire

```bash
# Requires AppHost project, orchestration, Visual Studio/VS Code
dotnet new aspire-starter
dotnet run --project MyApp.AppHost
```

### qyl

```bash
# Just run it
docker run -d -p 5100:5100 -p 4317:4317 ghcr.io/ancplua/qyl

# Or use hosted
export OTEL_EXPORTER_OTLP_ENDPOINT="https://qyl-api-production.up.railway.app"
```

## When to Use What

| Use Case | Recommendation |
|----------|----------------|
| Local .NET development with Aspire orchestration | Aspire 13.1 |
| Production observability (data must survive restarts) | qyl |
| GenAI/LLM workloads with auto-instrumentation | qyl |
| Multi-language microservices (Python, Go, Java) | qyl |
| Quick debugging, no AppHost needed | qyl |
| Full IDE integration + GitHub Copilot | Aspire 13.1 |
| Azure Functions with KEDA scaling | Aspire 13.1 |
| Resource dependency visualization | Aspire 13.1 |
| Historical telemetry queries | qyl (DuckDB) |

## Architecture

```
┌─────────────────┐                    ┌─────────────────┐
│  Your .NET App  │                    │  Any OTel App   │
│ (servicedefaults)                    │  (Python, Go..) │
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
   │ (persist)│  │ (React)  │  │ (agents) │
   └──────────┘  └──────────┘  └──────────┘
```

## Links

- **qyl**: https://github.com/ANcpLua/qyl
- **Hosted**: https://qyl-api-production.up.railway.app
- **Aspire Dashboard Docs**: https://learn.microsoft.com/dotnet/aspire/dashboard/overview
