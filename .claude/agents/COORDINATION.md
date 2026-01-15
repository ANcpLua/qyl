# qyl Agent Coordination

How the 3 agents work together.

## agents

```yaml
qyl-collector:
  domain: Backend (C#)
  owns: src/qyl.collector/, src/qyl.protocol/
  model: opus
  
qyl-dashboard:
  domain: Frontend (React)
  owns: src/qyl.dashboard/
  model: opus
  
qyl-build:
  domain: Infrastructure (NUKE)
  owns: eng/build/, core/specs/, Dockerfile
  model: opus
```

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

```yaml
build-time:
  1. qyl-build: TypeSpec → OpenAPI
  2. qyl-build: OpenAPI → C#/TS/DuckDB
  3. qyl-dashboard: npm run build → dist/
  4. qyl-build: dist/ → wwwroot/ (embed)
  5. qyl-collector: dotnet publish (includes wwwroot/)
  6. qyl-build: Docker image

runtime:
  1. qyl-collector: receives OTLP, stores in Ring Buffer + DuckDB
  2. qyl-collector: serves REST API + SSE stream
  3. qyl-dashboard: consumes API, displays forensics
```

## communication-protocol

```yaml
shared-contracts:
  source: core/openapi/openapi.yaml
  c#-types: src/qyl.protocol/**/*.g.cs
  ts-types: src/qyl.dashboard/src/types/api.ts
  rule: ALL agents use same types, never manual edits

when-schema-changes:
  1. qyl-build: updates TypeSpec in core/specs/
  2. qyl-build: runs `nuke Generate --force-generate`
  3. qyl-collector: receives new *.g.cs, adapts code
  4. qyl-dashboard: receives new api.ts, adapts components

when-api-changes:
  1. qyl-collector: updates endpoint implementation
  2. qyl-build: if contract changed, update TypeSpec first
  3. qyl-dashboard: adapts to new API shape
```

## ownership-boundaries

```yaml
qyl-collector-owns:
  - SpanRingBuffer implementation
  - DuckDB queries
  - OTLP parsing
  - REST endpoint handlers
  - SSE broadcast logic

qyl-dashboard-owns:
  - React components
  - Frontend RingBuffer
  - TanStack Query hooks
  - UI/UX decisions
  - Chart configurations

qyl-build-owns:
  - TypeSpec schemas
  - Code generators
  - NUKE targets
  - Dockerfile
  - CI/CD pipeline

nobody-owns (generated):
  - *.g.cs files
  - openapi.yaml
  - api.ts
  - DuckDbSchema.g.cs
```

## conflict-resolution

```yaml
if-type-needs-change:
  owner: qyl-build
  process: update TypeSpec, regenerate
  
if-api-needs-change:
  owner: qyl-build (schema) + qyl-collector (implementation)
  process: TypeSpec first, then implementation
  
if-ui-needs-new-data:
  owner: qyl-dashboard (request) → qyl-collector (implement)
  process: dashboard asks, collector provides
```

## parallel-work-strategy

```yaml
safe-parallel:
  - qyl-collector: Ring Buffer internals
  - qyl-dashboard: UI components
  - qyl-build: NUKE target improvements
  
requires-coordination:
  - Schema changes (all agents affected)
  - New API endpoints (build + collector)
  - New data types (build + collector + dashboard)
```

## verification

```yaml
before-merge:
  - `nuke Generate` produces no diff (schema in sync)
  - `npm run build` succeeds (dashboard builds)
  - `dotnet build` succeeds (collector builds)
  - `dotnet test` passes (all tests green)
  - `nuke DockerBuild` succeeds (container builds)
```
