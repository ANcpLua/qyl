# Architecture Decision Records (ADRs)

> Vertical Slice definitions for qyl. Each slice = one feature end-to-end.

## Document Hierarchy

```
CLAUDE.md                          <-- Root AI context
    |
    +-- docs/qyl-architecture.yaml <-- Architecture spec (SSOT)
    |
    +-- docs/MIGRATION_MASTER.yaml <-- Build repair + execution
    |
    +-- docs/architecture/decisions/  <-- Feature ADRs (this folder)
```

## Slice Registry

| ADR                                  | Slice | Name            | Prio | Depends On   | Status |
|--------------------------------------|-------|-----------------|------|--------------|--------|
| [0002](0002-vs01-span-ingestion.md)  | VS-01 | Span Ingestion  | P0   | -            | Draft  |
| [0003](0003-vs02-list-sessions.md)   | VS-02 | List Sessions   | P0   | VS-01        | Draft  |
| [0004](0004-vs03-view-trace-tree.md) | VS-03 | View Trace Tree | P1   | VS-01        | Draft  |
| [0005](0005-vs04-genai-analytics.md) | VS-04 | GenAI Analytics | P1   | VS-01, VS-02 | Draft  |
| [0006](0006-vs05-live-streaming.md)  | VS-05 | Live Streaming  | P2   | VS-01        | Draft  |
| [0007](0007-vs06-mcp-query-tool.md)  | VS-06 | MCP Query Tool  | P2   | VS-01, VS-02 | Draft  |

## Dependency Graph

```
VS-01 ──► VS-02 ──► VS-04
  │         └────► VS-06
  ├──► VS-03
  └──► VS-05
```

## Implementation Order

| Phase              | Slices       | Goal                                   |
|--------------------|--------------|----------------------------------------|
| P0 (Foundation)    | VS-01, VS-02 | Core ingestion + session listing       |
| P1 (Core Features) | VS-03, VS-04 | Trace visualization + GenAI analytics  |
| P2 (Enhancement)   | VS-05, VS-06 | Real-time streaming + AI agent tooling |

## What is a Vertical Slice?

```
TypeSpec → Storage → Query → API → MCP → Dashboard
              One Feature through all Layers
```

Each ADR defines:

- **Layers** - Which files belong to the slice (with code examples)
- **Acceptance Criteria** - When is the slice done (checkboxes)
- **Test Files** - Unit and integration tests
- **Risks** - Potential issues and mitigations

## Layer Locations

| Layer     | Path                           | Purpose                     |
|-----------|--------------------------------|-----------------------------|
| TypeSpec  | `core/specs/`                  | Contract-first API + Schema |
| Storage   | `src/qyl.collector/Storage/`   | DuckDB Schema + Store       |
| Query     | `src/qyl.collector/Query/`     | Aggregation SQL             |
| API       | `src/qyl.collector/Program.cs` | REST + SSE Endpoints        |
| MCP       | `src/qyl.mcp/Tools/`           | AI Agent Tools              |
| Dashboard | `src/qyl.dashboard/src/`       | React Components            |

## ADR Status Values

| Status      | Meaning                                 |
|-------------|-----------------------------------------|
| Draft       | Design documented, not implemented      |
| Accepted    | Design approved, implementation started |
| Implemented | Code complete, tests passing            |
| Deprecated  | Superseded by another ADR               |

## References

- [CLAUDE.md](../../../CLAUDE.md) - Root AI context
- [qyl-architecture.yaml](../qyl-architecture.yaml) - Full architecture spec
- [MIGRATION_MASTER.yaml](../MIGRATION_MASTER.yaml) - Build repair tasks
