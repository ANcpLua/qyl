# qyl — AI Observability Platform

OTLP-native observability: ingest traces/logs/metrics, store in DuckDB, query via API/MCP/Copilot.
Docker image IS the product.

## Core Rules

- When you learn something new about the codebase, update this file. This is YOUR FILE.
- If something doesn't make sense architecturally or product-wise, add it to `Requests to Humans` below.
- Always follow established coding patterns and conventions in the codebase.

## Behavioral Contract

**Be opinionated.** Suggest the right solution, not the quick one. If you see a bad assumption or a better route, say it plainly instead of agreeing with my framing.

**Think whole-system.** Every change touches the whole platform. If a collector change affects protocol types, fix protocol too. If a schema change affects DuckDB DDL, update the migration. Never scope to "only this file."

**Checkpoints, not partial completions.** For work spanning more than 2-3 files: propose a plan first, ask where to pause, execute in segments. Never half-finish and call it done.

**No suppression.** If you cannot root-fix a diagnostic, stop. No `#pragma warning disable`, no `[SuppressMessage]`, no `<NoWarn>`. If the diagnostic is wrong, explain why and we decide together.

**Map before changing.** Before modifying any pipeline or data flow: map current state, map proposed state, highlight the delta. If workflow stays the same, say so explicitly.

## Dependency Chain

```text
core/specs/*.tsp → qyl.protocol → qyl.collector → qyl.dashboard
                                 → qyl.mcp
                                 → qyl.servicedefaults → qyl.servicedefaults.generator
eng/build/ → orchestrates everything above
```

## Architecture

```text
              +------------------+
              |   qyl.dashboard  |
              |    (React 19)    |
              +--------+---------+
                       | HTTP
                       v
+----------+  +------------------+  +------+
| qyl.mcp  |->|  qyl.collector   |<-| OTLP |
| (stdio)  |  |  (ASP.NET Core)  |  |Clients|
+----------+  +--------+---------+  +------+
                       |
                       v
              +------------------+
              |     DuckDB       |
              +------------------+
```

## Project Map

| Directory | Purpose |
|-----------|---------|
| `core/specs/` | TypeSpec schemas (source of truth — never edit `*.g.cs`) |
| `eng/build/` | NUKE build system (11 files) |
| `src/qyl.collector/` | Backend: REST API, gRPC OTLP, SSE, DuckDB storage |
| `src/qyl.protocol/` | Shared types (BCL-only, zero dependencies) |
| `src/qyl.servicedefaults/` | OTel instrumentation SDK ([Traced], [GenAi], [Db]) |
| `src/qyl.servicedefaults.generator/` | Roslyn source generator for instrumentation |
| `src/qyl.instrumentation.generators/` | DuckDB schema + GenAI interceptor generators |
| `src/qyl.dashboard/` | React 19 + Vite 7 + Tailwind 4 + shadcn/ui |
| `src/qyl.browser/` | Browser OTLP SDK (TypeScript, ESM + IIFE) |
| `src/qyl.mcp/` | MCP server for AI agent queries |
| `src/qyl.copilot/` | GitHub Copilot extensibility |
| `src/qyl.hosting/` | .NET Aspire-style hosting |
| `src/qyl.watch/` | Terminal SSE span viewer |
| `tests/` | xUnit v3 + MTP (E2E via Copilot SDK harness) |

## Dependency Rules

```yaml
allowed:
  collector -> protocol (ProjectReference)
  mcp -> protocol (ProjectReference)
  dashboard -> collector (HTTP at runtime)
  mcp -> collector (HTTP at runtime)
forbidden:
  mcp -> collector (ProjectReference)    # must use HTTP
  protocol -> any-package                # must stay BCL-only
```

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 10.0 LTS, C# 14, net10.0 |
| Frontend | React 19, Vite 7, Tailwind CSS 4 |
| Storage | DuckDB (columnar, glibc required) |
| Protocol | OTel Semantic Conventions 1.40 |
| Testing | xUnit v3, Microsoft Testing Platform |
| Build | NUKE |

## Banned Patterns

| Do not use | Use instead |
|-----------|-------------|
| `DateTime.Now` / `DateTime.UtcNow` | `TimeProvider.System.GetUtcNow()` |
| `object _lock` | `Lock _lock = new()` |
| `Newtonsoft.Json` | `System.Text.Json` |
| `#pragma warning disable` | Fix the diagnostic |
| `[SuppressMessage]` | Fix the diagnostic |
| Raw `dotnet test` with MTP flags | `nuke Test` with parameters |
| `dotnet build` in CI | `nuke Ci` or `nuke Full` |

## Environment Variables

Only one agent variable: `export COPILOT_AGENT=true` (enables Copilot SDK test harness).
All other env vars are runtime config for the collector.

## Ports

| Port | Protocol | Purpose |
|------|----------|---------|
| 5100 | HTTP | REST API, SSE, Dashboard |
| 4317 | gRPC | OTLP traces/logs/metrics |
| 5173 | HTTP | Dashboard dev server |

## CI

GitHub Actions (`ci.yml`): backend (.NET) + frontend (React) + coverage + dependency audit.
Locally, `nuke Ci` replicates the backend pipeline. `nuke Full` adds frontend + codegen + verify.

## Requests to Humans

- [ ] ...
