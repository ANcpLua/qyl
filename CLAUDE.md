# qyl — AI Observability Platform

@Version.props

OTLP-native observability: ingest traces/logs/metrics, store in DuckDB, query via API/MCP/Copilot.
Docker image IS the product.

## Core Rules

- When you learn something non-obvious, update MEMORY.md or this file.
- Always follow established coding patterns and conventions in the codebase.
- If something doesn't make sense architecturally, add it to Requests to Humans below.

## Docs-First Workflow

- Start from the relevant slice doc or active plan doc before changing code.
- Work step by step through active docs.
- When a plan/roadmap/temp execution doc is completed or superseded, move it to `docs/done/`.
- Do NOT move prompts, slice `spec.md`, slice `requirements.md`, decisions, references, screenshots, or canonical guidance docs into `docs/done/`.

## Assistant Workflow Reference Pack (qyl-native)

- Workflow index: `.claude/qyl-workflows/qyl-skill-tree.md`
- Workflow router: `.claude/qyl-workflows/qyl-workflow.md`
- Natural-language query command: `/qyl` → `.claude/qyl-workflows/qyl-command.md`
- PR code review workflow: `.claude/qyl-workflows/qyl-code-review.md`
- PR review loop workflow: `.claude/qyl-workflows/qyl-pr-code-review.md`
- Issue remediation workflow: `.claude/qyl-workflows/qyl-fix-issues.md`
- Supporting notes and conventions: `.claude/qyl-workflows/AGENTS.md` and `.claude/qyl-workflows/README.md`
- Legacy mirror (for agent tooling compatibility): `.agents/qyl-workflows/`

## 3-Layer Architecture Model

| Layer | Location | Runs at | Responsibility |
|-------|----------|---------|----------------|
| 1. Schema generation | `eng/build/SchemaGenerator.cs` | NUKE build time | TypeSpec OpenAPI → C# models, enums, DuckDB DDL |
| 2. Roslyn source generation | `src/qyl.servicedefaults.generator/` | MSBuild compile time | 7 interceptor pipelines → compile-time instrumentation |
| 3. Runtime + collector | `src/qyl.servicedefaults/` + `src/qyl.collector/` | Application runtime | OTel wiring, collector discovery, DevLogs bridge, OTLP ingestion, DuckDB, SSE |

### Non-Negotiable Rules

- Do not confuse schema generation with Roslyn source generation.
- Do not treat compile-time interception as runtime reflection.
- Do not treat SchemaGenerator.cs as part of the Roslyn generator pipeline.
- Do not modify layers 1/2/3 unless the failing behavior is proven to originate there.
- Prefer fixes in feature/service layers first (dashboard, mcp, Loom services).

## Architecture

```text
CopilotKit / Angular / Vanilla JS
       ↕  AG-UI protocol (SSE)
              +------------------+
              |   qyl.dashboard  |
              |    (React 19)    |
              +--------+---------+
                       | HTTP
                       v
+----------+  +------------------+  +------+
| qyl.mcp  |->|  qyl.collector   |<-| OTLP |
| (stdio)  |  |  (ASP.NET Core)  |  |Clients|
+----------+  +--+-----------+---+  +------+
                 |           |
                 v           v
       +----------+  +-------------+
       |  DuckDB  |  | qyl.copilot |
       +----------+  +------+------+
                            |
                            v
                     QylAgentBuilder
                     → AIAgent (instrumented)
                     → InstrumentedChatClient
                     → GitHub Copilot / Azure OpenAI / Ollama
```

## Dependency Chain

```text
core/specs/*.tsp → qyl.protocol → qyl.collector → qyl.dashboard
                                 → qyl.mcp
                                 → qyl.copilot (AG-UI + declarative workflows)
                                 → qyl.servicedefaults → qyl.servicedefaults.generator
eng/build/ → orchestrates everything above
```

## Dependency Rules

```yaml
allowed:
  collector -> protocol (ProjectReference)
  collector -> copilot (ProjectReference)
  mcp -> protocol (ProjectReference)
  copilot -> protocol (ProjectReference)
  dashboard -> collector (HTTP at runtime)
  mcp -> collector (HTTP at runtime)
forbidden:
  mcp -> collector (ProjectReference)    # must use HTTP
  protocol -> any-package                # must stay BCL-only
  copilot -> collector (ProjectReference) # copilot is a library, not a host
```

## Tech Stack (training-prior overrides)

| Layer     | Technology                                    |
|-----------|-----------------------------------------------|
| Runtime   | .NET 10.0 LTS, C# 14, net10.0                |
| Frontend  | React 19, Vite 7, Tailwind CSS 4              |
| Storage   | DuckDB (columnar, glibc required)             |
| Protocol  | OTel Semantic Conventions 1.40                |
| Testing   | xUnit v3, Microsoft Testing Platform          |
| Build     | NUKE                                          |

## Environment Variables

| Variable        | Default    | Purpose                            |
|-----------------|------------|------------------------------------|
| `QYL_PORT`      | 5100       | Dashboard + REST API port          |
| `QYL_GRPC_PORT` | 4317       | gRPC OTLP port (0=disable)         |
| `QYL_OTLP_PORT` | 4318       | HTTP OTLP port (0=disable)         |
| `QYL_DATA_PATH` | qyl.duckdb | DuckDB file path                   |
| `PORT`          | —          | Railway/PaaS fallback for QYL_PORT |

## Key Design Docs

| Doc | Purpose |
|-----|---------|
| `docs/plans/2026-03-03-qyl-agui-declarative-design.md` | AG-UI + declarative workflows design (approved) |
| `docs/plans/2026-03-03-qyl-agui-declarative-impl.md` | AG-UI implementation plan |
| `docs/roadmap/loom-design.md` | Sentry Loom reverse-engineered spec + qyl implementation evidence |

## Requests to Humans

- [ ] ...
