# qyl Overview

OTLP-native AI observability platform. Ingest traces, logs, and metrics. Store in DuckDB. Query via REST API, MCP server, and Copilot.

Single Docker image. Single process. No external dependencies beyond the apps sending telemetry.

---

## 1. Architecture

```text
Browsers / Apps / Agents
       |
       | OTLP (gRPC :4317 or HTTP :4318)
       v
+------------------+
|  qyl.collector   |  ASP.NET Core
|  (single process)|
+--+----------+----+
   |          |
   v          v
DuckDB    qyl.agents
           |
           v
        AIAgent
        (QylAgentBuilder)
           |
           v
     LLM Providers
     (Copilot / Azure OpenAI / Ollama)
```

External surfaces:

```text
qyl.dashboard (React 19)  ←  HTTP :5100  →  qyl.collector
qyl.mcp (stdio or HTTP)   ←  HTTP        →  qyl.collector
qyl.loom (standalone)      ←  ProjectRef  →  qyl.collector + agents + workflows
```

## 2. Tech Stack

- .NET 10.0 LTS, C# 14, net10.0
- React 19, Vite 7, Tailwind CSS 4
- DuckDB (columnar, glibc required)
- OTel Semantic Conventions 1.40
- xUnit v3, Microsoft Testing Platform
- NUKE build system

## 3. Deployment

Single Docker image packages collector + dashboard static files.

Environment variables:

- `QYL_PORT` (default 5100) — dashboard + REST API
- `QYL_GRPC_PORT` (default 4317) — gRPC OTLP, 0 to disable
- `QYL_OTLP_PORT` (default 4318) — HTTP OTLP, 0 to disable
- `QYL_DATA_PATH` (default qyl.duckdb) — DuckDB file path
- `PORT` — Railway/PaaS fallback for QYL_PORT

## 4. Dependency Chain

```text
core/specs/*.tsp → qyl.contracts → qyl.collector → qyl.dashboard
                                  → qyl.mcp
                                  → qyl.agents + qyl.workflows
                                  → qyl.instrumentation → qyl.instrumentation.generators
                                  → qyl.collector.storage.generators
                                  → qyl.loom
eng/build/ → orchestrates everything
```

Dependency rules:

- mcp → collector: HTTP only, never ProjectReference
- contracts → nothing: BCL-only, zero packages
- instrumentation.generators ↔ collector.storage.generators: no cross-reference (DDD boundary)
- collector → loom: forbidden (loom depends on collector, not vice versa)

## 5. 3-Layer Build Model

Layer 1 — Schema generation. `eng/build/SchemaGenerator.cs`. Runs at NUKE build time. TypeSpec OpenAPI → C# models, enums, DuckDB DDL.

Layer 2 — Roslyn source generation. `src/qyl.instrumentation.generators/`. Runs at MSBuild compile time. 7 interceptor pipelines → compile-time instrumentation.

Layer 3 — Runtime. `src/qyl.instrumentation/` + `src/qyl.collector/`. Runs at application startup. OTel wiring, collector discovery, OTLP ingestion, DuckDB storage, SSE streaming.

Do not confuse layers. Schema generation is not Roslyn generation. Compile-time interception is not runtime reflection.

## 6. Banned APIs

- `DateTime.Now` / `DateTime.UtcNow` → `TimeProvider.System.GetUtcNow()`
- `object _lock` → `Lock _lock = new()`
- `Newtonsoft.Json` → `System.Text.Json`
- `#pragma warning disable`, `[SuppressMessage]`, `<NoWarn>` → fix the code
- `ISourceGenerator` → `IIncrementalGenerator`
- `SyntaxFactory.NormalizeWhitespace()` → raw strings
- Runtime reflection, `dynamic`, `ExpandoObject`, `.Result`, `.Wait()`

## 7. Build and Run

```bash
# Build
nuke

# Test
nuke test

# Run collector
dotnet run --project src/qyl.collector

# Run dashboard dev server
cd src/qyl.dashboard && npm run dev
```
