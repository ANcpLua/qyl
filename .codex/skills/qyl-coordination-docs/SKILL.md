---
name: qyl-coordination-docs
description: |
  Coordination guide for qyl agent collaboration
---

## Source Metadata

```yaml
# none
```


# qyl Agent Coordination

How the 3 agents work together.

## agents

| Agent | Domain | Skills |
|-------|--------|--------|
| `qyl-collector` | C# Backend | `/docs-lookup`, `/review` |
| `qyl-dashboard` | React Frontend | `/frontend-design`, `/review` |
| `qyl-build` | NUKE/TypeSpec | `/slice-validate`, `/type-ownership` |

## data-flow

```
                    qyl-build
                        │
         ┌──────────────┼──────────────┐
         │              │              │
         ▼              ▼              ▼
    *.g.cs         api.ts        DuckDbSchema.g.cs
         │              │              │
         ▼              │              ▼
    qyl-collector ◄─────┘         qyl-dashboard
         │                             │
         └──────── REST/SSE ───────────┘
```

## dependency-chain

**Build-time:**
1. `qyl-build`: TypeSpec → OpenAPI
2. `qyl-build`: OpenAPI → C#/TS/DuckDB
3. `qyl-dashboard`: npm build → dist/
4. `qyl-build`: dist/ → wwwroot/ (embed)
5. `qyl-collector`: dotnet publish
6. `qyl-build`: Docker image

**Runtime:**
1. `qyl-collector`: receives OTLP, stores in RingBuffer + DuckDB
2. `qyl-collector`: serves REST API + SSE stream
3. `qyl-dashboard`: consumes API, displays forensics

## ownership-rules

| Agent | Owns | Never Touches |
|-------|------|---------------|
| `qyl-collector` | SpanRingBuffer, DuckDB queries, REST handlers | UI, TypeSpec |
| `qyl-dashboard` | React components, TanStack hooks, charts | Backend, *.g.cs |
| `qyl-build` | TypeSpec, code generators, NUKE targets | Runtime code |

**Generated files (no agent owns):**
- `*.g.cs`, `openapi.yaml`, `api.ts`, `DuckDbSchema.g.cs`

## communication-protocol

**Shared contracts:**
```yaml
source: core/openapi/openapi.yaml
c#-types: src/qyl.protocol/**/*.g.cs
ts-types: src/qyl.dashboard/src/types/api.ts
rule: ALL agents use same types, never manual edits
```

**When schema changes:**
1. `qyl-build`: updates TypeSpec
2. `qyl-build`: runs `nuke Generate --force-generate`
3. `qyl-collector`: adapts to new *.g.cs
4. `qyl-dashboard`: adapts to new api.ts

## parallel-work

**Safe (independent):**
- `qyl-collector`: RingBuffer internals
- `qyl-dashboard`: UI components
- `qyl-build`: NUKE target improvements

**Requires coordination:**
- Schema changes (all agents)
- New API endpoints (build + collector)
- New data types (all agents)

## verification

Before merge, ALL must pass:
```bash
nuke Generate        # No diff (schema in sync)
npm run build        # Dashboard builds
dotnet build         # Collector builds
dotnet test          # All tests green
nuke DockerBuild     # Container builds
```
