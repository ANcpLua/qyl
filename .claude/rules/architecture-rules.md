---
paths:
  - "src/**/*.cs"
  - "**/*.csproj"
---

# Architecture Rules

## Type Ownership

Types are organized by responsibility and dependency direction:

| Project | Constraint | Purpose |
|---------|-----------|---------|
| `qyl.protocol` | **BCL only** (zero packages) | Shared types used by 2+ projects |
| `qyl.collector` | Can reference protocol | Backend implementation |
| `qyl.mcp` | Can reference protocol | MCP server implementation |
| `qyl.dashboard` | **Independent** | Frontend (no .NET deps) |

**Rule**: If a type is used by multiple projects, it MUST live in `qyl.protocol`.

## Dependency Rules

```yaml
allowed:
  - from: collector, mcp
    to: protocol
    via: ProjectReference

  - from: dashboard
    to: collector
    via: HTTP (runtime only)

  - from: mcp
    to: collector
    via: HTTP (runtime only)

forbidden:
  - from: mcp
    to: collector
    via: ProjectReference
    reason: must communicate via HTTP for decoupling

  - from: protocol
    to: any-external-package
    reason: must remain BCL-only leaf

  - from: dashboard
    to: any-dotnet
    reason: pure frontend build artifact
```

## Vertical Slice Pattern

Each feature should have complete end-to-end implementation:

```
Feature: GenAI Token Tracking
├── TypeSpec (schema)
├── C# Types (protocol)
├── DuckDB Schema (storage)
├── REST API (collector)
├── TypeScript Types (dashboard)
└── React UI (dashboard)
```

Use `/slice-validate` skill to verify completeness.

## Single Source of Truth

```yaml
schema: core/specs/*.tsp (TypeSpec)
  ↓
generated:
  - core/openapi/openapi.yaml
  - src/qyl.protocol/**/*.g.cs
  - src/qyl.collector/Storage/DuckDbSchema.g.cs
  - src/qyl.dashboard/src/types/api.ts

rule: NEVER edit generated files
```
