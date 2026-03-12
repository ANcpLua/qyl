# qyl — AI Observability Platform

@Version.props

OTLP-native observability: ingest traces/logs/metrics, store in DuckDB, query via API/MCP/Copilot.
Deploys as Docker container (self-hosted) or cloud service (HTTP + OAuth).

## Core Rules

- When you learn something non-obvious, update MEMORY.md or this file.
- If something doesn't line up or exist twice suggest the human a better approach.

## Docs-First Workflow

- Start from the relevant slice doc or active plan doc before changing code.
- Work step by step through active docs.
- When a plan/roadmap/temp execution doc is completed or superseded, move it to `docs/done/`.
- Keep the overall structure very cohesive avoid extreme programming - be smart not lazy.

## Assistant Workflow Reference Pack (qyl)

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
| 2. Roslyn source generation | `src/qyl.instrumentation.generators/` | MSBuild compile time | 7 interceptor pipelines → compile-time instrumentation |
| 3. Runtime + collector | `src/qyl.instrumentation/` + `src/qyl.collector/` | Application runtime | OTel wiring, collector discovery, DevLogs bridge, OTLP ingestion, DuckDB, SSE |

### Non-Negotiable Rules

- Do not confuse schema generation with Roslyn source generation.
- Do not treat compile-time interception as runtime reflection.
- Do not treat SchemaGenerator.cs as part of the Roslyn generator pipeline.
- Do not modify layers 1/2/3 unless the failing behavior is proven to originate there.
- Prefer fixes in feature/service layers first (dashboard, mcp, Loom services).
- Loom uses AIAgent (via QylAgentBuilder), not raw IChatClient calls.

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
| (stdio + |  |  (ASP.NET Core)  |  |Clients|
|  HTTP)   |  +--+-----------+---+  +------+
+----------+     |           |
                 v           v
       +----------+  +------------+
       |  DuckDB  |  | qyl.agents |
       +----------+  +-----+------+
                           |
                           v
                    QylAgentBuilder
                    → AIAgent (instrumented)
                    → InstrumentedChatClient
                    → GitHub Copilot / Azure OpenAI / Ollama
```

**qyl.mcp deployment modes:**
- **Embedded** — runs inside qyl.collector, direct DI access via `IQylDataService`
- **Standalone stdio** — CLI tool (`dotnet tool install qyl-mcp`), token auth
- **Standalone HTTP** — cloud service, OAuth 2.0 + PKCE, MCP 1.1.0 auth

## Dependency Chain

```text
core/specs/*.tsp → qyl.contracts → qyl.collector → qyl.dashboard
                                  → qyl.mcp
                                  → qyl.agents + qyl.workflows
                                  → qyl.instrumentation → qyl.instrumentation.generators
                                  → qyl.collector.storage.generators (DuckDB codegen)
eng/build/ → orchestrates everything above
```

## Dependency Rules

```yaml
allowed:
  collector -> contracts (ProjectReference)
  collector -> agents (ProjectReference)
  collector -> workflows (ProjectReference)
  collector -> instrumentation (ProjectReference)
  collector -> mcp (ProjectReference)               # embedded mode
  mcp -> contracts (ProjectReference)
  mcp -> IQylDataService abstraction                # direct DI (embedded) or HTTP client (standalone)
  loom -> collector, agents, workflows, contracts, instrumentation (ProjectReference)
  dashboard -> collector (HTTP at runtime)
forbidden:
  contracts -> any-package                           # must stay BCL-only
  instrumentation.generators -> collector/storage    # DDD boundary
  collector.storage.generators -> instrumentation    # DDD boundary
```

## Tech Stack (training-prior overrides)

| Layer     | Technology                                    |
|-----------|-----------------------------------------------|
| Runtime   | .NET 10.0 LTS, C# 14, net10.0                |
| Frontend  | React 19, Vite 7, Tailwind CSS 4              |
| Storage   | DuckDB (columnar, glibc required)             |
| Protocol  | OTel Semantic Conventions 1.40                |
| MCP       | ModelContextProtocol.AspNetCore 1.1.0         |
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
| `docs/instrumentation-toolkit.md` | Canonical Roslyn generator reference (12 attributes, 6 pipelines) |
| `docs/loom-design.md` | Architecture reference + reverse-engineered spec |
| `docs/mcp-tool-audit.md` | MCP tool verification matrix (78 tools, 27 classes) |

## Requests to Humans

- [ ] Missing project-specific `MEMORY.md` process for architecture discoveries.
- [ ] Add explicit acceptance criteria for architecture changes that cross collector/loom/mcp boundaries.
