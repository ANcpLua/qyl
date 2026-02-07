# qyl - AI Observability Platform

> **Audience:** Codex, Gemini, and humans. NOT auto-loaded by Claude Code.
> Claude's context: `CLAUDE.md` (auto-loaded) + `src/*/CLAUDE.md` per component.

## Decision Tree

```
Task received
  |
  +-- Writing .NET/C# code?
  |     +-- .NET 10.0 LTS, C# 14, net10.0 TFM
  |     +-- Source generators: qyl.servicedefaults.generator (GenAI/Db/Traced interceptors)
  |     +-- OTel: semconv 1.39 (gen_ai.*, db.*, http.*)
  |     +-- Time: TimeProvider.System.GetUtcNow() — never use current-time via DateTime
  |     +-- Locking: Lock (sync), SemaphoreSlim (async) — never object _lock
  |     +-- JSON: System.Text.Json — never Newtonsoft
  |     +-- Protocol: BCL-only. Zero packages in qyl.protocol.
  |     +-- Never edit *.g.cs — edit TypeSpec and regenerate
  |
  +-- Writing React/TS?
  |     +-- React 19 + Vite 7 + Tailwind CSS 4
  |     +-- Types from src/qyl.dashboard/src/types/api.ts (generated — never edit)
  |     +-- shadcn/ui components
  |
  +-- Editing schemas or types?
  |     +-- Edit core/specs/*.tsp (TypeSpec is SSOT)
  |     +-- Run: nuke Generate --force-generate
  |     +-- NEVER edit *.g.cs, api.ts, DuckDbSchema.g.cs
  |
  +-- Building?
  |     +-- nuke Compile — build all
  |     +-- nuke Test — xUnit v3, Microsoft Testing Platform
  |     +-- nuke Generate — codegen from TypeSpec + semconv
  |     +-- nuke DockerBuild — container image (requires glibc, no Alpine)
  |
  +-- Debugging/observability?
  |     +-- Traces: qyl.get_trace, qyl.search_spans
  |     +-- Errors: qyl.analyze_session_errors, qyl.list_errors
  |     +-- GenAI: qyl.get_genai_stats, qyl.list_genai_spans, qyl.list_models
  |     +-- Logs: qyl.search_logs, qyl.list_structured_logs
  |     +-- Latency: qyl.get_latency_stats (P50/P95/P99)
  |     +-- Sessions: qyl.list_sessions, qyl.get_session_transcript
```

## Architecture

```
Claude Code / Codex / Gemini
  | (MCP stdio)
  v
qyl.mcp (22 tools) ---- HTTP ----> qyl.collector (ASP.NET Core)
                                      |
                                  DuckDB (spans, logs, sessions, errors)
                                  OTLP :4317 (gRPC ingestion)
                                  REST :5100 (API + SSE)
                                      |
qyl.dashboard (React 19, served at :5100)
```

### Components

| Component | Path | Purpose |
|-----------|------|---------|
| collector | `src/qyl.collector/` | OTLP ingestion, REST API, SSE, DuckDB storage |
| dashboard | `src/qyl.dashboard/` | React 19 + Vite 7 + Tailwind 4 frontend |
| mcp | `src/qyl.mcp/` | MCP server (stdio, 22 tools for AI agents) |
| protocol | `src/qyl.protocol/` | Shared types, BCL-only, no packages |
| copilot | `src/qyl.copilot/` | GitHub Copilot integration |
| hosting | `src/qyl.hosting/` | Hosting abstractions + telemetry |
| servicedefaults | `src/qyl.servicedefaults/` | Aspire-style service defaults |
| servicedefaults.generator | `src/qyl.servicedefaults.generator/` | Roslyn source generator (GenAI interceptors) |
| instrumentation.generators | `src/qyl.instrumentation.generators/` | DuckDB insert + interceptor generators |

## Codegen

```
nuke Generate
  |
  core/specs/*.tsp --> openapi.yaml --> C# records, DuckDB DDL, TS types
  eng/semconv/     --> OTel semconv --> C#, TS, TypeSpec, DuckDB SQL
```

## MCP Tools (22, all prefixed qyl.)

Sessions: `list_sessions` `get_session_transcript` | Traces: `get_trace` `search_spans` | Errors: `analyze_session_errors` `list_errors` | GenAI: `get_genai_stats` `list_genai_spans` `list_models` `get_token_timeseries` | Agent Runs: `search_agent_runs` `get_agent_run` `get_token_usage` `get_latency_stats` | Logs: `list_structured_logs` `list_trace_logs` `search_logs` | Console: `list_console_logs` `list_console_errors` | Health: `health_check` `get_storage_stats`

## Docs Index

| Path | Content |
|------|---------|
| `CLAUDE.md` | Architecture, build, env vars, ports |
| `eng/CLAUDE.md` | NUKE build system |
| `core/CLAUDE.md` | TypeSpec schema details |
| `src/qyl.collector/CLAUDE.md` | Collector internals |
| `src/qyl.mcp/CLAUDE.md` | MCP server, tool implementations |
| `src/qyl.dashboard/CLAUDE.md` | Frontend architecture |
| `src/qyl.protocol/CLAUDE.md` | Shared types |
| `src/qyl.copilot/CLAUDE.md` | Copilot integration |
| `src/qyl.hosting/CLAUDE.md` | Hosting abstractions |
| `src/qyl.servicedefaults/CLAUDE.md` | Service defaults |
| `src/qyl.servicedefaults.generator/CLAUDE.md` | Source generator |
| `src/qyl.instrumentation.generators/CLAUDE.md` | DuckDB generators |
| `tests/CLAUDE.md` | Test conventions |

## Plugin (new, unproven)

`.claude-plugin/` contains 3 commands (deploy, logs, setup) and 3 skills.
`.qyl/workflows/` contains 3 workflow templates (analyze-errors, model-comparison, trace-summary).

## Ports

| Port | Service |
|------|---------|
| 5100 | HTTP (collector + dashboard) |
| 4317 | gRPC OTLP ingestion |
| 5173 | Dashboard dev server (Vite) |
