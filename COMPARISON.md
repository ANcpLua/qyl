# qyl vs Aspire Dashboard

**tl;dr:** qyl is Aspire dashboard with persistent storage, zero-config GenAI instrumentation, and no orchestrator required.

## Feature Comparison

| Feature | qyl | Aspire Dashboard |
|---------|-----|------------------|
| **OTLP Ingestion** | gRPC (4317) + HTTP (5100) | gRPC + HTTP |
| **Storage** | DuckDB (persistent, columnar) | In-memory only |
| **Dashboard** | React 19 embedded | Blazor |
| **Real-time Streaming** | SSE (`/api/v1/live`) | SignalR |
| **Traces/Spans** | Full OTel 1.39 | Full OTel |
| **GenAI Telemetry** | Native + auto-instrumentation | Manual (since 13.1) |
| **Idempotent Ingestion** | Upsert on span_id | No |
| **Source Generator** | Zero-config interceptors | Manual instrumentation |
| **MCP Server** | AI agent queries | Since 13.1 |
| **Docker** | Single container | Single container |
| **Standalone Mode** | Always standalone | Optional |
| **Restart Persistence** | Yes (DuckDB) | No (lost on restart) |

## What Aspire Has That qyl Doesn't (Yet)

| Feature | Notes |
|---------|-------|
| Resources Page | No AppHost concept in qyl |
| Console Logs | qyl has structured logs only |
| GitHub Copilot | IDE integration required |
| Start/Stop Resources | Not relevant (standalone) |
| Auth (OIDC, BrowserToken) | qyl currently unsecured |
| Resource Graph View | Visual dependency graph |
| Keyboard Shortcuts | Dashboard navigation |

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
| Local .NET development with Aspire | Aspire Dashboard |
| Production observability | qyl (persistent) |
| GenAI/LLM workloads | qyl (native support) |
| Multi-language microservices | qyl (language-agnostic) |
| Quick debugging, no setup | qyl (single container) |
| Full IDE integration + Copilot | Aspire Dashboard |

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
