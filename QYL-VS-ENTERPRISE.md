# qyl Platform Reference

## Signal Flow

```
OTLP in --> qyl.collector --> DuckDB --> qyl.dashboard
            one binary        one file    one UI
```

Three signal types (traces, logs, metrics) enter through OTLP, land in columnar storage, render through a single dashboard.

---

## Component Registry

| Component | Role | Protocol | Port |
|-----------|------|----------|------|
| **qyl.collector** | OTLP ingestion, storage, query API, SSE streaming | gRPC + HTTP | 4317 / 5100 |
| **qyl.dashboard** | React 19 SPA: charts, waterfalls, agent traces, 3D viz, live streaming | HTTP | 5173 (dev) |
| **qyl.hosting** | Service orchestration framework (`QylApp`, resource model) | — | — |
| **qyl.protocol** | TypeSpec-generated shared types, BCL-only | — | — |
| **qyl.servicedefaults** | OTel instrumentation SDK | — | — |
| **qyl.servicedefaults.generator** | Roslyn source gen: `[Traced]`, `[GenAi]`, `[Db]` compile-time interceptors | — | — |
| **qyl.instrumentation.generators** | DuckDB schema + GenAI interceptor generators | — | — |
| **qyl.mcp** | MCP server for AI agent telemetry queries (stdio, AOT) | MCP/stdio | — |
| **qyl.copilot** | GitHub Copilot extensibility agent | HTTP | — |
| **qyl.browser** | Browser SDK: Web Vitals, interactions, navigation timing (ESM + IIFE) | OTLP/HTTP | — |
| **qyl.watch** | Live terminal span viewer (dotnet tool, SSE) | SSE | — |
| **qyl.watchdog** | Process anomaly detection daemon (dotnet tool) | — | — |
| **qyl.cli** | One-command instrumentation setup for .NET/Docker projects | CLI | — |
| **qyl.Analyzers** | 15 Roslyn analyzers (QYL001-015), compile-time telemetry validation | — | — |
| **qyl.Analyzers.CodeFixes** | Automated code fix providers for QYL diagnostics | — | — |

---

## How Signals Move

### Traces
OTLP protobuf (gRPC :4317 or HTTP :5100) -> `OtlpProtobufParser` -> batch insert (100 spans/batch) -> DuckDB `spans` table. Correlated by `trace_id` and `parent_span_id`.

### Logs
OTLP log records -> DuckDB `logs` table. Correlated to traces via `trace_id` + `span_id`. Structured attributes preserved as JSON.

### Metrics
OTLP metrics -> DuckDB. Same store, same query interface, same dashboard panels.

### Agent Runs
Spans with `gen_ai.agent.name` attribute -> extracted into `agent_runs` table. Tool calls extracted into `tool_calls` table with `sequence_number` ordering.

### Errors
Spans with `status_code = ERROR` -> grouped by error message -> ranked by frequency -> surfaced in dashboard Issues panel. No separate pipeline.

### Browser Signals
`qyl.browser` SDK captures Web Vitals, user interactions, navigation timing -> OTLP/HTTP to same collector -> same DuckDB -> same dashboard. A single trace can span browser click through API call through database query through response render.

---

## Instrumentation Model

```
source code + [Traced] / [GenAi] / [Db] attributes
        |
        v (compile time)
qyl.servicedefaults.generator (Roslyn source gen)
        |
        v
interceptor code emitted directly into binary
        |
        v (runtime)
OTel SDK -> OTLP -> qyl.collector
```

Instrumentation is resolved at build time. No runtime reflection. AOT-compatible. The generated code behaves as if hand-written.

---

## Storage Model

DuckDB. Columnar. Embedded. One `.duckdb` file.

| Table | Stores | Key Columns |
|-------|--------|-------------|
| `spans` | Every span from every trace | `span_id`, `trace_id`, `parent_span_id`, `name`, `kind`, `start_time_unix_nano`, `end_time_unix_nano`, `status_code`, `attributes` (JSON) |
| `logs` | Structured log records | `trace_id`, `span_id`, `severity_number`, `body`, `attributes` |
| `agent_runs` | Extracted agent invocations | `run_id`, `trace_id`, `agent_name`, `model`, `status`, `input_tokens`, `output_tokens`, `total_cost`, `tool_call_count` |
| `tool_calls` | Individual tool executions | `call_id`, `run_id`, `tool_name`, `arguments_json`, `result_json`, `status`, `duration_ns`, `sequence_number` |

Timestamps stored as `UBIGINT` (passed as `decimal` for DuckDB.NET compatibility). Single writer connection for all writes (channel-buffered), pooled read connections.

Schema versioned: `collectorSchemaVersion` constant in `Program.cs`, migrations applied via `MigrationRunner.ApplyPendingMigrations`. Migration SQL files in `src/qyl.collector/Storage/Migrations/*.sql`.

---

## Query Interface

### REST API (:5100)
```
GET  /api/v1/traces          # Trace list with aggregated columns
GET  /api/v1/traces/:id      # Single trace with full span tree
GET  /api/v1/spans           # Span search
GET  /api/v1/logs            # Log search
GET  /api/v1/agents          # Agent run list
GET  /api/v1/live            # SSE stream (real-time spans)
```

### MCP (qyl.mcp, stdio)
AI agents query telemetry through natural language via MCP protocol. The MCP server translates queries to DuckDB SQL against the same store. Connects to collector via HTTP (`QYL_COLLECTOR_URL`).

### Copilot (qyl.copilot)
IDE-integrated telemetry queries through GitHub Copilot extensibility.

---

## Dashboard Surfaces

| Route | Renders |
|-------|---------|
| `/agents` | Agent runs overview: traffic, duration, issues, LLM calls, tokens, tool calls (6-panel grid) + trace list + abbreviated trace waterfall |
| `/genai` | Token tracking, cost analysis, provider stats |
| `/traces` | Span hierarchy, waterfall timeline |
| `/logs` | Structured log search and correlation |

Dashboard talks directly to its own collector. No external data source configuration.

---

## AI-Native Capabilities

**qyl.mcp** -- MCP server (stdio transport, AOT, `PackAsTool`). Telemetry tools, replay, structured log queries. Autonomous agents can self-diagnose through their own telemetry.

**qyl.copilot** -- Copilot extensibility agent. Developers query observability data without leaving the editor.

**qyl.dashboard /agents** -- Purpose-built for agentic workloads: which tools the agent called, how many tokens each LLM call consumed, cost per invocation, sub-agent spawn points. Agent sessions rendered as trace waterfalls with tool call sequencing.

**qyl.Analyzers** -- 15 compile-time rules that catch telemetry mistakes before the code runs. Diagnostics QYL001 through QYL015 with automated code fixes.

---

## Environment

| Variable | Default | Purpose |
|----------|---------|---------|
| `QYL_PORT` | 5100 | HTTP API |
| `QYL_GRPC_PORT` | 4317 | gRPC OTLP (0=disable) |
| `QYL_DATA_PATH` | ./qyl.duckdb | Storage location |
| `QYL_TOKEN` | (auto-generated) | Auth token |
| `QYL_MAX_RETENTION_DAYS` | 30 | Telemetry retention |
| `QYL_MAX_SPAN_COUNT` | 1000000 | Span limit before cleanup |
| `QYL_MAX_LOG_COUNT` | 500000 | Log limit before cleanup |
| `QYL_CLEANUP_INTERVAL_SECONDS` | 300 | Cleanup interval |
| `QYL_OTLP_AUTH_MODE` | Unsecured | OTLP auth (Unsecured/ApiKey) |
| `QYL_COLLECTOR_URL` | http://localhost:5100 | MCP -> Collector |

---

## TypeSpec-First Types

```
core/specs/*.tsp -> tsp compile -> openapi.yaml -> C# | DuckDB | TypeScript | JSON
```

All types originate in `core/specs/`. Generated outputs: `*.g.cs`, `api.ts`, `openapi.yaml`. Never edit generated files.

---

## Build

```bash
dotnet run --project src/qyl.collector          # Backend
cd src/qyl.dashboard && npm run dev             # Frontend
nuke Full                                        # Complete pipeline
nuke Generate --force-generate                   # Regenerate all types
dotnet test                                      # Tests (xUnit v3, MTP)
```
